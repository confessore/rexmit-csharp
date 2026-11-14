# Native host operations

## First install

Use an Ubuntu 26.04 amd64 host with systemd and network access. Clone with `--recurse-submodules`, then run `sudo bash scripts/rexmit install` from the checkout. Install prompts for a missing Discord token and does not fetch or change the source revision.

## Update and recovery

Update the configured checkout as its owner, including the pinned submodule, then run `sudo rexmit update`. The manager builds before stopping the service, verifies downloaded native assets, records the source and submodule revisions, activates the release, and waits for a `REXMIT_READY` marker from the active systemd process. A failed build leaves the current release untouched; a failed start/readiness check restores the prior release. `sudo rexmit rollback` performs the same activation checks against the retained prior release.

Updates restart the service and interrupt current playback. Queues are in memory and are not backed up. The previous release is a code rollback only, not a data backup.

The interactive player panel is also process-local. After a restart, use `/panel` to publish a new panel. Clear and disconnect controls ask the acting member to confirm; playback controls require that member to be in the bot's voice channel.

## Diagnostics

- `sudo rexmit doctor` checks the token, media tools, current executable, and service state.
- `sudo rexmit version` shows the active revision and installed media-tool versions.
- `sudo rexmit status` shows systemd state.
- `sudo rexmit logs` shows the last 100 journal lines; `sudo rexmit logs --follow` streams logs.

The service restarts on failure. Review `sudo journalctl -u rexmit.service -n 100 --no-pager` before repeated restarts.

## Uninstall and package cleanup

`sudo rexmit uninstall` stops and removes the service, managed app/tool files, and dedicated account. It deliberately preserves the token, management command, and apt packages. Only after uninstall, `sudo rexmit remove-deps` simulates package removal, checks that the plan contains only packages recorded as newly installed by Rexmit, and asks for confirmation. It never invokes `apt autoremove`. `purge-config` and `purge-manager` are separate, explicit actions.

## Validation

Run `bash scripts/check.sh` before deployment. It builds and tests the solution, verifies first-party formatting without rewriting the pinned upstream submodule, and runs shell syntax and management tests. The shell tests validate readiness matching and package-removal safeguards without changing the host. Validate a fresh install/update/rollback/uninstall cycle on a disposable Ubuntu 26.04 amd64 host before production rollout.
