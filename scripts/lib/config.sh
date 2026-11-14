#!/usr/bin/env bash

setup_config() {
    local token confirmation
    if run_as_root test -f "$ENV_FILE" && ! confirm "Replace the saved Discord token? Other config entries will be preserved."; then
        printf 'Configuration unchanged.\n'
        return
    fi

    read -r -s -p "Discord bot token: " token
    printf '\n'
    read -r -s -p "Confirm Discord bot token: " confirmation
    printf '\n'
    [[ -n $token ]] || die "The Discord token cannot be empty."
    [[ $token == "$confirmation" ]] || die "The token entries did not match."
    [[ $token =~ ^[A-Za-z0-9._-]+$ ]] || die "The token contains unsupported characters. No config was written."

    printf '%s\n' "$token" | run_as_root bash -c '
        set -Eeuo pipefail
        config_dir=/etc/rexmit
        config_file="$config_dir/rexmit.env"
        install -d -o root -g root -m 0700 "$config_dir"
        temp_file=$(mktemp "$config_dir/.rexmit.env.XXXXXX")
        trap '\''rm -f -- "$temp_file"'\'' EXIT
        if [[ -f $config_file ]]; then
            awk '\''!/^DISCORD_TOKEN=/'\'' "$config_file" > "$temp_file"
        fi
        IFS= read -r token
        printf "DISCORD_TOKEN=%s\n" "$token" >> "$temp_file"
        chown root:root "$temp_file"
        chmod 0600 "$temp_file"
        mv -f -- "$temp_file" "$config_file"
    '
    printf 'Token saved to %s with root-only permissions.\n' "$ENV_FILE"
}

purge_config() {
    require_root purge-config
    [[ ! -f $SERVICE_FILE ]] || die "Uninstall the bot before purging its config."
    [[ -d $CONFIG_DIR ]] || {
        printf 'No Rexmit config directory exists.\n'
        return
    }
    if ! confirm "Permanently delete the saved Discord token and Rexmit config in $CONFIG_DIR?"; then
        printf 'Config purge cancelled.\n'
        return
    fi
    rm -f -- "$ENV_FILE"
    rmdir "$CONFIG_DIR" 2>/dev/null || true
    printf 'Saved bot token deleted; manager source configuration was kept if present.\n'
}
