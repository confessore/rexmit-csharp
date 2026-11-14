# Architecture and host layout

Rexmit is one .NET application with a native systemd host. `Program.cs` composes a Generic Host; `Hosting/` configures Discord and service lifecycle; `Modules/Playback/` exposes Discord commands and renders the player panel; `Services/` coordinates queues and panel refreshes; `Playback/` owns each guild's playback worker; and `Media/` validates URLs, reads track metadata, and runs media tools.

## Deployment boundaries

| Path | Owner / access | Purpose |
| --- | --- | --- |
| `/opt/rexmit/releases/<release>` | root, read-only to service | Immutable self-contained application and `libdave.so` releases |
| `/opt/rexmit/current` | root | Symlink to the active release |
| `/opt/rexmit/previous` | root | Symlink to the last known-good release, retained for rollback |
| `/opt/rexmit/tools/bin` | root, executable by service | Pinned Deno and yt-dlp tools |
| `/etc/rexmit/rexmit.env` | root-only | Discord token, read by systemd and passed to the service |
| `/etc/rexmit/manager.conf` | root-only | Source checkout used for future builds |
| `/usr/local/bin/rexmit` | root-owned executable | Management command |
| `/usr/local/lib/rexmit` | root-owned, read-only modules | Management functions and systemd template, available without the checkout |
| `/var/lib/rexmit` | `rexmit-bot` | Writable service state and home directory |
| `/var/lib/rexmit-manager/apt-packages` | root-only | Explicit apt packages first installed by Rexmit |

The bot runs as a locked service account with systemd filesystem and privilege restrictions. Builds run as that account in an isolated temporary source/build directory. The source tree's main-project working changes are copied into a release; the Discord.Net submodule must be initialized, pinned to the recorded gitlink, and clean.

## Configuration inventory

| Variable | Required | Source | Handling |
| --- | --- | --- | --- |
| `DISCORD_TOKEN` | Yes | Discord Developer Portal | Root-only systemd environment file; never logged |

The application uses no database, HTTP listener, persistent user data, or environment-specific role configuration. Interactive panel message references are in memory; after a restart, use `/panel` to publish a fresh panel.
