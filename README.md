# Rexmit YouTube Music Bot

A Discord music bot with an in-server interactive player panel. Playback queues and status are in memory, so they reset when the process restarts. Rexmit needs no database or web server.

## Production installation

The supported deployment is a **native Ubuntu 26.04 amd64 host using systemd**. Containers are not supported or required. The installer creates a locked `rexmit-bot` service account, installs FFmpeg and voice libraries, publishes a self-contained .NET 10 application, installs pinned Deno/yt-dlp binaries, and configures a hardened systemd unit.

1. Create a bot in the [Discord Developer Portal](https://discord.com/developers/applications). Enable the `bot` and `applications.commands` scopes, and grant View Channels, Send Messages, Embed Links, Connect, and Speak permissions.
2. Clone the repository with its pinned Discord.Net source: `git clone --recurse-submodules <repository-url>`.
3. From the checkout on the Ubuntu host, run `sudo bash scripts/rexmit install`. If no token is configured, the installer prompts without echo and saves it under `/etc/rexmit` with root-only permissions.

To change the token, run `sudo rexmit setup`. The service reads `/etc/rexmit/rexmit.env`; do not put tokens in the checkout or command line.

## Management

```sh
sudo rexmit status
sudo rexmit doctor
sudo rexmit logs                 # most recent 100 lines
sudo rexmit logs --follow
sudo rexmit restart              # waits for Discord readiness
sudo rexmit version
```

To deploy source changes, update the configured checkout and pinned submodule as the checkout owner, then run `sudo rexmit update`. The manager builds the checkout without running Git operations, stages a release, switches the current symlink, and waits for the new process to report Discord readiness. If startup or readiness fails, it restores the previous release. One previous release is retained; `sudo rexmit rollback` switches back to it explicitly.

`sudo rexmit update-deps` installs supported apt dependencies and refreshes the pinned Deno/yt-dlp tools. Tool binaries and the pinned `libdave` archive are checksum-verified before installation. The .NET 10 SDK is installed only when a source publish needs it and is removed after a successful publish if Rexmit installed it.

`sudo rexmit uninstall` removes the service, application releases, managed media tools, and dedicated account, while preserving `/etc/rexmit` and apt packages. Cleanup is explicit: `sudo rexmit remove-deps` simulates apt removal and offers to remove only packages recorded as newly installed by Rexmit; it never runs `apt autoremove`. `sudo rexmit purge-config` separately prompts before deleting the saved token. `sudo rexmit purge-manager` removes the global management command and its installed modules.

The manager supports Ubuntu 26.04 amd64 with systemd and apt. Install/update require network access to apt, NuGet, and pinned GitHub releases. Keep the configured source checkout available for builds; management of an installed service does not otherwise depend on the checkout.

## Development and checks

Requirements: .NET 10 SDK and the initialized Discord.Net submodule.

```sh
git submodule update --init --recursive
bash scripts/check.sh
```

For local execution, set `DISCORD_TOKEN` in the process environment and run `dotnet run --project rexmit.csproj`. Never commit a real token.

## Commands

- `/play <YouTube URL>` — join your voice channel and add one video.
- `/panel` — post or refresh the interactive player in the current channel.
- `/queue` and `/nowplaying` — inspect playback.
- `/skip`, `/remove <position>`, and `/clear` — manage the queue.
- `/leave` — stop playback and disconnect.
- `/help` — show command descriptions.

The player panel shows the current title, artist/uploader, thumbnail, requester, queue, and playback position. Members in the bot's voice channel can add tracks with the panel form, pause/resume, skip, shuffle, move or remove queued tracks, and clear the queue. Queue clearing and disconnect ask for confirmation. `/leave` and the panel's disconnect action are available even after you leave voice; a later `/play` reconnects if the previous voice session became stale. Position is measured from PCM audio sent to Discord and the panel refreshes about every 10 seconds.

Each server has its own player and a maximum of 25 waiting tracks. Playlist links are treated as a single video, not expanded into a playlist. Only HTTPS YouTube and youtu.be links are accepted.

When no human members remain in the bot's voice channel, Rexmit stops playback and disconnects after five minutes. If a human returns during that grace period, the cleanup is canceled; other bots do not count as human members.

Playback status is exposed through the read-only `IAudioStatusReader` interface. No HTTP listener is started; an in-process host can consume the reader without gaining playback controls or requiring a database. A separate web process would need a read-only transport to the bot.

Voice connections use Discord.Net with DAVE end-to-end encryption enabled. Until the next stable Discord.Net package includes the required DAVE fix, this repository pins upstream commit `2db165815cbb29846ffb914a475698db5f645b82` as a source submodule. Native installation includes the pinned Linux `libdave` binary; manual deployments must provide a compatible `libdave.so` in the application directory or system library path.

YouTube does not provide a general official API for streaming music into Discord. Playback depends on yt-dlp's YouTube support and may stop working when YouTube changes its service; make sure your use complies with applicable terms and copyright rules.
