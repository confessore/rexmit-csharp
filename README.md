# Rexmit YouTube Music Bot

A small Discord music bot. Its only required secret is a Discord bot token; playback uses `yt-dlp`, Deno, and `ffmpeg` at runtime.

## Run with Docker Compose

1. Create a bot in the [Discord Developer Portal](https://discord.com/developers/applications), copy its bot token, and invite it with the `bot` and `applications.commands` scopes. Grant it View Channels, Send Messages, Connect, and Speak permissions.
2. Copy `.env.example` to `.env` and set `DISCORD_TOKEN`.
3. Run `docker compose up --build -d`.

The bot registers slash commands globally at startup. Discord may take some time to propagate new or updated global commands.

## Commands

- `/play <YouTube URL>` — join your voice channel and add one video.
- `/queue` and `/nowplaying` — inspect playback.
- `/skip`, `/remove <position>`, and `/clear` — manage the queue.
- `/leave` — stop playback and disconnect.
- `/help` — show command descriptions.

Each server has its own player and a maximum of 25 waiting tracks. Playlist links are treated as a single video, not expanded into a playlist. Only HTTPS YouTube and youtu.be links are accepted.

Playback and queue status are held in memory and reset when the bot restarts. The bot exposes status through the read-only `IAudioStatusReader` interface, but does not start an HTTP server. A future web host running in the same process can use that interface without gaining playback controls or requiring a database; a separate web process would need a read-only transport between it and the bot.

## Local run

Install .NET 8, `ffmpeg`, `yt-dlp`, and Deno 2.3.0 or newer, set `DISCORD_TOKEN`, then run `dotnet run`. On PowerShell, set the token for the current session with `$env:DISCORD_TOKEN = "your-token"`.

YouTube does not provide a general official API for streaming music into Discord. Playback depends on yt-dlp's YouTube support and may stop working when YouTube changes its service; make sure your use complies with applicable terms and copyright rules.
