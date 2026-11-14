#!/usr/bin/env bash

install_manager() {
    local manager_source manager_temp lib_source module
    manager_source=$SOURCE_DIR/scripts/rexmit
    [[ -f $manager_source ]] || manager_source=$SCRIPT_PATH
    lib_source=$SOURCE_DIR/scripts/lib
    [[ -d $lib_source ]] || lib_source=$SCRIPT_DIR/lib
    [[ -f $lib_source/common.sh && -f $SOURCE_DIR/scripts/systemd/rexmit.service ]] ||
        die "Management modules or the systemd service template are missing from the source checkout."

    if [[ -e $MANAGER_BIN ]] && ! grep -Fq '# REXMIT_MANAGEMENT_SCRIPT=1' "$MANAGER_BIN"; then
        die "$MANAGER_BIN already exists and is not managed by Rexmit."
    fi
    install -d -o root -g root -m 0755 "$MANAGER_LIB_DIR"
    for module in "$lib_source"/*.sh; do
        install -o root -g root -m 0644 "$module" "$MANAGER_LIB_DIR/$(basename -- "$module")"
    done
    install -o root -g root -m 0644 "$SOURCE_DIR/scripts/systemd/rexmit.service" "$MANAGER_LIB_DIR/rexmit.service"
    printf 'Rexmit management modules\n' > "$MANAGER_LIB_DIR/INSTALLER"
    chown root:root "$MANAGER_LIB_DIR/INSTALLER"
    chmod 0644 "$MANAGER_LIB_DIR/INSTALLER"

    manager_temp=$(mktemp /usr/local/bin/.rexmit.XXXXXX)
    install -o root -g root -m 0755 "$manager_source" "$manager_temp"
    mv -f -- "$manager_temp" "$MANAGER_BIN"

    install -d -o root -g root -m 0700 "$CONFIG_DIR"
    manager_temp=$(mktemp "$CONFIG_DIR/.manager.conf.XXXXXX")
    printf '%s\n' "$SOURCE_DIR" > "$manager_temp"
    chown root:root "$manager_temp"
    chmod 0600 "$manager_temp"
    mv -f -- "$manager_temp" "$MANAGER_CONFIG"
}

install_bot() {
    require_root install
    check_target
    install -d -o root -g root -m 0755 "$APP_ROOT" "$RELEASES_DIR"
    if [[ ! -f $ENV_FILE ]] || ! grep -Eq '^DISCORD_TOKEN=[A-Za-z0-9._-]+$' "$ENV_FILE"; then
        setup_config
    fi
    check_config
    check_source
    ensure_apt_dependencies
    install_audio_tools
    ensure_service_user
    publish_release
    install_service_unit
    install_manager
    systemctl enable rexmit.service
    activate_release "$BUILT_RELEASE"
    printf 'Installed and started Rexmit. Use `rexmit status` and `rexmit logs`.\n'
}

update_bot() {
    require_root update
    check_target
    check_config
    [[ -f $SERVICE_FILE ]] || die "Rexmit is not installed. Run 'rexmit install' first."
    check_source
    publish_release
    install_manager
    install_service_unit
    activate_release "$BUILT_RELEASE"
    printf 'Updated Rexmit from %s.\n' "$SOURCE_DIR"
}

update_dependencies() {
    require_root update-deps
    check_target
    install -d -o root -g root -m 0755 "$APP_ROOT"
    ensure_apt_dependencies
    install_audio_tools
    if systemctl is-active --quiet rexmit.service; then
        restart_service_and_wait
    fi
    printf 'Dependencies updated (Deno %s, yt-dlp %s).\n' "$DENO_VERSION" "$YT_DLP_VERSION"
}

uninstall_bot() {
    require_root uninstall
    if ! confirm "Stop Rexmit and remove its service, installed app, tools, and service account? Config and apt packages will be kept."; then
        printf 'Uninstall cancelled.\n'
        return
    fi

    systemctl disable --now rexmit.service 2>/dev/null || true
    rm -f -- "$SERVICE_FILE"
    systemctl daemon-reload
    rm -rf -- "$APP_ROOT" /var/lib/rexmit
    if getent passwd rexmit-bot >/dev/null; then
        userdel rexmit-bot
    fi
    if getent group rexmit-bot >/dev/null; then
        groupdel rexmit-bot 2>/dev/null || true
    fi
    printf 'Uninstalled Rexmit. Management command and config remain at %s; apt packages remain installed.\n' "$CONFIG_DIR"
}

purge_manager() {
    require_root purge-manager
    [[ ! -f $SERVICE_FILE ]] || die "Uninstall the bot before removing its management command."
    if ! confirm "Remove the global Rexmit management command and modules? Saved config and apt packages will remain."; then
        printf 'Management command removal cancelled.\n'
        return
    fi
    if grep -Fq '# REXMIT_MANAGEMENT_SCRIPT=1' "$MANAGER_BIN" 2>/dev/null; then
        rm -f -- "$MANAGER_BIN"
    fi
    if [[ -f $MANAGER_LIB_DIR/INSTALLER ]] && grep -Fq 'Rexmit management modules' "$MANAGER_LIB_DIR/INSTALLER"; then
        rm -f -- "$MANAGER_LIB_DIR"/*.sh "$MANAGER_LIB_DIR/rexmit.service" "$MANAGER_LIB_DIR/INSTALLER"
        rmdir "$MANAGER_LIB_DIR" 2>/dev/null || true
    fi
    rm -f -- "$MANAGER_CONFIG"
    rmdir "$CONFIG_DIR" 2>/dev/null || true
    printf 'Management command removed. Saved config and apt packages remain.\n'
}
