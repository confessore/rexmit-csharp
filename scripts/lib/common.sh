#!/usr/bin/env bash

readonly APP_ROOT=/opt/rexmit
readonly RELEASES_DIR=$APP_ROOT/releases
readonly CURRENT_LINK=$APP_ROOT/current
readonly PREVIOUS_LINK=$APP_ROOT/previous
readonly TOOLS_DIR=$APP_ROOT/tools/bin
readonly CONFIG_DIR=/etc/rexmit
readonly ENV_FILE=$CONFIG_DIR/rexmit.env
readonly MANAGER_CONFIG=$CONFIG_DIR/manager.conf
readonly SERVICE_FILE=/etc/systemd/system/rexmit.service
readonly MANAGER_BIN=/usr/local/bin/rexmit
readonly MANAGER_LIB_DIR=/usr/local/lib/rexmit
readonly STATE_DIR=/var/lib/rexmit-manager
readonly PACKAGE_MANIFEST=$STATE_DIR/apt-packages
readonly APT_PACKAGES=(ca-certificates curl ffmpeg git libicu-dev libopus-dev libsodium-dev unzip)
readonly TRACKED_APT_PACKAGES=(curl ffmpeg git libicu-dev libopus-dev libsodium-dev unzip)
readonly LIBDAVE_URL=https://github.com/discord/libdave/releases/download/v1.2.1/cpp/libdave-Linux-X64-boringssl.zip
readonly LIBDAVE_SHA256=7fdf10a7df894406291463130a322c04d7de58085b602500866a11874e8907b4
readonly DENO_VERSION=2.9.8
readonly DENO_SHA256=e890975bc2549807bbcc0b584ffafda72cc7c93349dd99c80483dd784f6c4609
readonly YT_DLP_VERSION=2026.10.26
readonly YT_DLP_SHA256=767ba9dce7052aa196f8b12652f433d76de128ba168966fa84b6394d5e6c6795

SOURCE_DIR=$SCRIPT_ROOT
if [[ ! -f $SOURCE_DIR/rexmit.csproj && -f $MANAGER_CONFIG ]]; then
    IFS= read -r SOURCE_DIR < "$MANAGER_CONFIG" || true
fi

TEMP_TOOL_DIR=
TEMP_RELEASE_DIR=
TEMP_BUILD_DIR=
BUILT_RELEASE=

cleanup() {
    [[ -z $TEMP_TOOL_DIR || ! -d $TEMP_TOOL_DIR ]] || rm -rf -- "$TEMP_TOOL_DIR"
    [[ -z $TEMP_RELEASE_DIR || ! -d $TEMP_RELEASE_DIR ]] || rm -rf -- "$TEMP_RELEASE_DIR"
    [[ -z $TEMP_BUILD_DIR || ! -d $TEMP_BUILD_DIR ]] || rm -rf -- "$TEMP_BUILD_DIR"
}
trap cleanup EXIT

die() {
    printf 'error: %s\n' "$*" >&2
    exit 1
}

usage() {
    cat <<'EOF'
Usage: rexmit <command> [--yes] [--follow]

Commands:
  setup         Prompt for and securely save the Discord token
  install       Install native dependencies, publish the bot, and enable systemd
  update        Build the checked-out source and activate a verified release
  update-deps   Update pinned Deno/yt-dlp tools and apt dependencies
  rollback      Restore the previous published release
  version       Show installed application and media-tool versions
  doctor        Check configuration, dependencies, service, and source checkout
  start         Start the systemd service and wait for Discord readiness
  stop          Stop the systemd service
  restart       Restart the service and wait for Discord readiness
  status        Show service status
  logs          Show recent service logs; add --follow to stream
  uninstall     Remove service, app files, and service account; preserve config
  remove-deps   Offer to remove only recorded packages installed by Rexmit
  purge-config  Separately confirm deletion of saved configuration/token
  purge-manager Remove the global management command and its modules

Install and update build from the configured checkout; Git revisions are not
fetched or changed by this command. Source releases are staged before activation.
EOF
}

require_root() {
    if (( EUID != 0 )); then
        die "Run this command with sudo, for example: sudo rexmit $1"
    fi
}

run_as_root() {
    if (( EUID == 0 )); then
        "$@"
    else
        command -v sudo >/dev/null 2>&1 || die "sudo is required to save configuration as root."
        sudo "$@"
    fi
}

confirm() {
    local answer
    if [[ ${ASSUME_YES:-0} == 1 ]]; then
        return 0
    fi
    read -r -p "$1 [y/N] " answer
    [[ $answer == y || $answer == Y ]]
}

check_target() {
    [[ -r /etc/os-release ]] || die "Cannot identify the operating system."
    # shellcheck disable=SC1091
    source /etc/os-release
    [[ ${ID:-} == ubuntu && ${VERSION_ID:-} == 26.04 ]] ||
        die "Native installation supports Ubuntu 26.04 only."
    [[ $(uname -m) == x86_64 ]] || die "Native installation currently supports amd64 (x86_64) only."
    command -v apt-get >/dev/null 2>&1 || die "apt-get is required."
    command -v systemctl >/dev/null 2>&1 || die "systemd is required."
}

check_source() {
    [[ -f $SOURCE_DIR/rexmit.csproj ]] || die "Source checkout not found. Run from the repository or check $MANAGER_CONFIG."
    local submodule expected actual state
    submodule=$SOURCE_DIR/third_party/Discord.Net
    [[ -f $submodule/src/Discord.Net.WebSocket/Discord.Net.WebSocket.csproj ]] ||
        die "Pinned Discord.Net source is missing. Run 'git -C $SOURCE_DIR submodule update --init --recursive' as the checkout owner."
    expected=$(git -C "$SOURCE_DIR" rev-parse HEAD:third_party/Discord.Net 2>/dev/null) ||
        die "Could not read the pinned Discord.Net submodule revision."
    actual=$(git -C "$submodule" rev-parse HEAD 2>/dev/null) ||
        die "Could not read the checked-out Discord.Net revision."
    [[ $actual == "$expected" ]] || die "Discord.Net submodule does not match the revision pinned by this checkout."
    state=$(git -C "$submodule" status --porcelain --untracked-files=all)
    [[ -z $state ]] || die "Discord.Net submodule has local changes; commit or discard them before publishing."
}

check_config() {
    [[ -f $ENV_FILE ]] || die "No bot config found. Run 'rexmit setup' first."
    [[ $(stat -c '%U:%G:%a' "$CONFIG_DIR") == root:root:700 ]] ||
        die "Configuration directory must be owned by root with mode 0700. Run 'rexmit setup' to repair it."
    [[ $(stat -c '%U:%G:%a' "$ENV_FILE") == root:root:600 ]] ||
        die "Bot configuration must be owned by root with mode 0600. Run 'rexmit setup' to repair it."
    grep -Eq '^DISCORD_TOKEN=[A-Za-z0-9._-]+$' "$ENV_FILE" ||
        die "DISCORD_TOKEN is missing or malformed. Run 'rexmit setup' again."
}

check_manager_lock() {
    require_root "$1"
    command -v flock >/dev/null 2>&1 || die "flock is required to safely manage the service."
    exec 9>/run/lock/rexmit-management.lock
    flock -n 9 || die "Another Rexmit management command is already running."
}
