#!/usr/bin/env bash

ensure_apt_dependencies() {
    local package status
    local newly_installed=()
    install -d -o root -g root -m 0755 "$STATE_DIR"
    touch "$PACKAGE_MANIFEST"
    chown root:root "$PACKAGE_MANIFEST"
    chmod 0600 "$PACKAGE_MANIFEST"

    for package in "${TRACKED_APT_PACKAGES[@]}"; do
        status=$(dpkg-query -W -f='${db:Status-Status}' "$package" 2>/dev/null || true)
        if [[ $status != installed ]]; then
            newly_installed+=("$package")
        fi
    done
    for package in "${newly_installed[@]}"; do
        grep -Fxq "$package" "$PACKAGE_MANIFEST" || printf '%s\n' "$package" >> "$PACKAGE_MANIFEST"
    done

    apt-get update
    DEBIAN_FRONTEND=noninteractive apt-get install -y --no-install-recommends "${APT_PACKAGES[@]}"
}

ensure_build_sdk() {
    local status
    status=$(dpkg-query -W -f='${db:Status-Status}' dotnet-sdk-10.0 2>/dev/null || true)
    [[ $status == installed ]] && return 0
    install -d -o root -g root -m 0755 "$STATE_DIR"
    touch "$PACKAGE_MANIFEST"
    chown root:root "$PACKAGE_MANIFEST"
    chmod 0600 "$PACKAGE_MANIFEST"
    grep -Fxq dotnet-sdk-10.0 "$PACKAGE_MANIFEST" || printf 'dotnet-sdk-10.0\n' >> "$PACKAGE_MANIFEST"
    apt-get update
    DEBIAN_FRONTEND=noninteractive apt-get install -y --no-install-recommends dotnet-sdk-10.0
}

remove_build_sdk() {
    local status
    grep -Fxq dotnet-sdk-10.0 "$PACKAGE_MANIFEST" 2>/dev/null || return 0
    status=$(dpkg-query -W -f='${db:Status-Status}' dotnet-sdk-10.0 2>/dev/null || true)
    [[ $status != installed ]] || apt-get remove -y --no-auto-remove dotnet-sdk-10.0
}

install_audio_tools() {
    local staging deno_archive yt_dlp_archive attempt
    TEMP_TOOL_DIR=$(mktemp -d "$APP_ROOT/.tools.XXXXXX")
    staging=$TEMP_TOOL_DIR
    deno_archive=$staging/deno.zip
    yt_dlp_archive=$staging/yt-dlp
    mkdir -p "$staging/unpacked" "$TOOLS_DIR"

    curl -fL --retry 3 --retry-delay 2 --retry-all-errors \
        "https://github.com/denoland/deno/releases/download/v$DENO_VERSION/deno-x86_64-unknown-linux-gnu.zip" \
        -o "$deno_archive"
    printf '%s  %s\n' "$DENO_SHA256" "$deno_archive" | sha256sum --check --status ||
        die "Downloaded Deno archive failed its SHA-256 check."
    unzip -q "$deno_archive" -d "$staging/unpacked"
    [[ -s $staging/unpacked/deno ]] || die "Deno executable is missing from the verified archive."

    curl -fL --retry 3 --retry-delay 2 --retry-all-errors \
        "https://github.com/yt-dlp/yt-dlp/releases/download/$YT_DLP_VERSION/yt-dlp_linux" \
        -o "$yt_dlp_archive"
    printf '%s  %s\n' "$YT_DLP_SHA256" "$yt_dlp_archive" | sha256sum --check --status ||
        die "Downloaded yt-dlp executable failed its SHA-256 check."

    chmod 0755 "$staging/unpacked/deno" "$yt_dlp_archive"
    for attempt in 1 2 3; do
        if "$staging/unpacked/deno" --version | grep -Fq "deno $DENO_VERSION" &&
            "$yt_dlp_archive" --version | grep -Fxq "$YT_DLP_VERSION"; then
            break
        fi
        sleep "$attempt"
    done
    "$staging/unpacked/deno" --version | grep -Fq "deno $DENO_VERSION" ||
        die "Downloaded Deno failed its version check."
    "$yt_dlp_archive" --version | grep -Fxq "$YT_DLP_VERSION" ||
        die "Downloaded yt-dlp failed its version check."

    install -o root -g root -m 0755 "$staging/unpacked/deno" "$TOOLS_DIR/.deno.new"
    install -o root -g root -m 0755 "$yt_dlp_archive" "$TOOLS_DIR/.yt-dlp.new"
    mv -f -- "$TOOLS_DIR/.deno.new" "$TOOLS_DIR/deno"
    mv -f -- "$TOOLS_DIR/.yt-dlp.new" "$TOOLS_DIR/yt-dlp"
    rm -rf -- "$staging"
    TEMP_TOOL_DIR=
}

remove_dependencies() {
    require_root remove-deps
    [[ ! -f $SERVICE_FILE ]] || die "Uninstall the bot before removing its audio/build packages."
    [[ -s $PACKAGE_MANIFEST ]] || {
        printf 'No apt packages are recorded as newly installed by Rexmit.\n'
        return
    }

    mapfile -t packages < "$PACKAGE_MANIFEST"
    local installed=() package status removal_plan
    for package in "${packages[@]}"; do
        status=$(dpkg-query -W -f='${db:Status-Status}' "$package" 2>/dev/null || true)
        [[ $status == installed ]] && installed+=("$package")
    done
    ((${#installed[@]} > 0)) || {
        rm -f -- "$PACKAGE_MANIFEST"
        printf 'Recorded apt packages are already absent.\n'
        return
    }

    removal_plan=$(apt-get -s remove --no-auto-remove -- "${installed[@]}") ||
        die "Could not simulate the requested apt package removal."
    if ! validate_package_removal_plan "$removal_plan" "${installed[@]}"; then
        die "APT would remove an unowned package; keeping all recorded packages."
    fi
    printf 'Packages recorded as newly installed: %s\n' "${installed[*]}"
    printf 'APT removal plan contains only these packages; apt autoremove will not run.\n'
    confirm "Remove these packages?" || {
        printf 'Dependency removal cancelled.\n'
        return
    }
    apt-get remove -y --no-auto-remove -- "${installed[@]}"
    rm -f -- "$PACKAGE_MANIFEST"
    printf 'Recorded apt packages removed.\n'
}

validate_package_removal_plan() {
    local plan=$1 action planned_package package owned
    shift
    local -a allowed_packages=("$@")
    while read -r action planned_package _; do
        [[ $action == Remv || $action == Purg ]] || continue
        owned=0
        for package in "${allowed_packages[@]}"; do
            if [[ $planned_package == "$package" || ${planned_package%%:*} == "${package%%:*}" ]]; then
                owned=1
                break
            fi
        done
        if ((owned != 1)); then
            printf 'APT would remove unowned package %q.\n' "$planned_package" >&2
            return 1
        fi
    done <<< "$plan"
}

show_versions() {
    local release revision
    release=$(readlink -f -- "$CURRENT_LINK" 2>/dev/null || true)
    if [[ -n $release && -r $release/REVISION ]]; then
        revision=$(<"$release/REVISION")
        printf 'Application: %s (%s)\n' "$revision" "$release"
    else
        printf 'Application: not installed\n'
    fi
    [[ ! -x $TOOLS_DIR/deno ]] || "$TOOLS_DIR/deno" --version | sed -n '1p'
    [[ ! -x $TOOLS_DIR/yt-dlp ]] || printf 'yt-dlp %s\n' "$("$TOOLS_DIR/yt-dlp" --version)"
    command -v ffmpeg >/dev/null 2>&1 && ffmpeg -version | sed -n '1p' || true
}

doctor() {
    local failed=0
    if check_config >/dev/null 2>&1; then
        printf 'PASS configuration exists and token is well-formed\n'
    else
        printf 'FAIL configuration missing or invalid (%s)\n' "$ENV_FILE"
        failed=1
    fi
    if check_source >/dev/null 2>&1; then
        printf 'PASS source checkout and pinned Discord.Net revision are ready\n'
    else
        printf 'FAIL source checkout or pinned Discord.Net revision is invalid (%s)\n' "$SOURCE_DIR"
        failed=1
    fi
    for package in ffmpeg; do
        if command -v "$package" >/dev/null 2>&1; then
            printf 'PASS %s is installed\n' "$package"
        else
            printf 'FAIL %s is missing\n' "$package"
            failed=1
        fi
    done
    for tool in deno yt-dlp; do
        if [[ -x $TOOLS_DIR/$tool ]]; then
            printf 'PASS %s is installed\n' "$tool"
        else
            printf 'FAIL %s is missing\n' "$tool"
            failed=1
        fi
    done
    if systemctl is-active --quiet rexmit.service; then
        printf 'PASS rexmit.service is active\n'
    else
        printf 'FAIL rexmit.service is not active\n'
        failed=1
    fi
    if [[ -L $CURRENT_LINK && -x $CURRENT_LINK/rexmit ]]; then
        printf 'PASS current application release is executable\n'
    else
        printf 'FAIL current application release is missing\n'
        failed=1
    fi
    if [[ -s $CURRENT_LINK/libdave.so ]]; then
        printf 'PASS current release contains libdave\n'
    else
        printf 'FAIL current release is missing libdave\n'
        failed=1
    fi
    return "$failed"
}
