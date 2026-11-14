# Rexmit YouTube Music Bot

A headless Discord music bot. It needs a Discord bot token, but no database or web server. Playback and queue status are in memory and reset when the bot restarts.

## Native Ubuntu/Debian install (amd64)

The management script installs a self-contained .NET 8 build, FFmpeg voice dependencies, Deno, yt-dlp, the pinned native `libdave` voice-encryption library, and a systemd service running as a dedicated `rexmit-bot` user. It installs the .NET SDK temporarily only when needed to build; the SDK is removed after publishing.

1. Create a bot in the [Discord Developer Portal](https://discord.com/developers/applications), copy its token, and invite it with the `bot` and `applications.commands` scopes. Grant it View Channels, Send Messages, Connect, and Speak permissions.
2. Clone this repository with its pinned Discord.Net source: `git clone --recurse-submodules <repository-url>` and enter the checkout.
3. Run `sudo bash scripts/rexmit install`. If no token is configured, it prompts for it without echoing and saves it in `/etc/rexmit/rexmit.env` with root-only permissions; then it installs and starts the service and installs the `rexmit` management command in `/usr/local/bin`.

To change the token later, run `sudo rexmit setup`.

Useful commands after installation:

```sh
sudo rexmit status
sudo rexmit logs
sudo rexmit restart
```

To deploy code updates, update the configured checkout to the revision you want, run `git submodule update --init --recursive` as the checkout owner, then run `sudo rexmit update`. Keep that checkout at its installed path. The command builds it, keeps the current and previous releases under `/opt/rexmit/releases`, and switches the service only after a successful publish. It does not fetch or change Git revisions for you.

`sudo rexmit update-deps` refreshes Deno and yt-dlp and checks apt dependencies. `sudo rexmit uninstall` removes the service, app files, tools, and service account, but preserves the management command, `/etc/rexmit`, and apt packages. Cleanup is explicit: after uninstall, `sudo rexmit remove-deps` offers to remove only named apt packages recorded as newly installed by Rexmit, without running `apt autoremove`; `sudo rexmit purge-config` separately prompts before deleting the saved token; `sudo rexmit purge-manager` removes the global management command when you are done with it.

The installer currently supports Ubuntu/Debian amd64 with systemd and apt. Run install/update commands with sudo. It requires network access to apt, NuGet, Deno, and GitHub release downloads.

## Docker Compose alternative

Initialize the pinned Discord.Net source with `git submodule update --init --recursive`, copy `.env.example` to `.env`, set `DISCORD_TOKEN`, then run `docker compose up --build -d`. The image includes .NET, FFmpeg, Deno, yt-dlp, and the pinned native `libdave` voice-encryption library (Linux amd64 and arm64). This is optional; the native install does not require Docker.

## Commands

- `/play <YouTube URL>` — join your voice channel and add one video.
- `/queue` and `/nowplaying` — inspect playback.
- `/skip`, `/remove <position>`, and `/clear` — manage the queue.
- `/leave` — stop playback and disconnect.
- `/help` — show command descriptions.

Each server has its own player and a maximum of 25 waiting tracks. Playlist links are treated as a single video, not expanded into a playlist. Only HTTPS YouTube and youtu.be links are accepted.

Playback status is exposed through the read-only `IAudioStatusReader` interface. No HTTP listener is started; a future in-process web host can consume the reader without gaining playback controls or requiring a database. A separate web process would need a read-only transport to the bot.

Voice connections use Discord.Net with DAVE end-to-end encryption enabled. Until the next stable Discord.Net package includes the DAVE MLS fix, this repository pins upstream commit `2db165815cbb29846ffb914a475698db5f645b82` as a source submodule. The native installer and Docker image include the pinned Linux `libdave` binary; manual deployments must provide a compatible `libdave.so` in the app directory or system library path.

YouTube does not provide a general official API for streaming music into Discord. Playback depends on yt-dlp's YouTube support and may stop working when YouTube changes its service; make sure your use complies with applicable terms and copyright rules.
