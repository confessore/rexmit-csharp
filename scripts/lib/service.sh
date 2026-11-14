#!/usr/bin/env bash

ensure_service_user() {
    local account home shell
    if account=$(getent passwd rexmit-bot); then
        home=$(cut -d: -f6 <<< "$account")
        shell=$(cut -d: -f7 <<< "$account")
        [[ $home == /var/lib/rexmit && $shell == */nologin ]] ||
            die "The rexmit-bot account exists but is not the expected locked service user."
    else
        useradd --system --user-group --create-home --home-dir /var/lib/rexmit --shell /usr/sbin/nologin rexmit-bot
    fi
    install -d -o rexmit-bot -g rexmit-bot -m 0750 /var/lib/rexmit
}

install_service_unit() {
    local unit_source=$SOURCE_DIR/scripts/systemd/rexmit.service
    if [[ ! -f $unit_source ]]; then
        unit_source=$MANAGER_LIB_DIR/rexmit.service
    fi
    [[ -f $unit_source ]] || die "The Rexmit systemd service template is missing."
    install -o root -g root -m 0644 "$unit_source" "$SERVICE_FILE"
    systemctl daemon-reload
}

wait_for_ready() {
    local timeout=${1:-120} elapsed pid
    for ((elapsed = 0; elapsed < timeout; elapsed += 3)); do
        if ! systemctl is-active --quiet rexmit.service; then
            break
        fi
        pid=$(systemctl show rexmit.service --property=MainPID --value 2>/dev/null || true)
        if [[ $pid =~ ^[1-9][0-9]*$ ]] &&
            journalctl -u rexmit.service -n 100 -o cat --no-pager 2>/dev/null | grep -Fxq "REXMIT_READY pid=$pid"; then
            return 0
        fi
        sleep 3
    done
    printf 'Rexmit did not become Discord-ready within %s seconds. Recent logs:\n' "$timeout" >&2
    journalctl -u rexmit.service -n 80 --no-pager >&2 || true
    return 1
}

start_service_and_wait() {
    systemctl start rexmit.service
    wait_for_ready
}

restart_service_and_wait() {
    systemctl restart rexmit.service
    wait_for_ready
}

show_status() {
    systemctl status rexmit.service --no-pager
}

show_logs() {
    if [[ ${FOLLOW_LOGS:-0} == 1 ]]; then
        journalctl -u rexmit.service -f
    else
        journalctl -u rexmit.service -n 100 --no-pager
    fi
}
