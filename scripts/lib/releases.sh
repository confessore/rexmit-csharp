#!/usr/bin/env bash

publish_release() {
    local source_build submodule_build staging commit submodule_commit dirty_marker
    check_source
    ensure_build_sdk
    command -v dotnet >/dev/null 2>&1 || die "Install the .NET 10 SDK package before publishing."
    dotnet --list-sdks | grep -Eq '^10\.' || die "The .NET 10 SDK is required to publish Rexmit."

    install -d -o root -g root -m 0755 "$RELEASES_DIR"
    TEMP_RELEASE_DIR=$(mktemp -d "$RELEASES_DIR/.staging.XXXXXX")
    TEMP_BUILD_DIR=$(mktemp -d /var/tmp/rexmit-build.XXXXXX)
    source_build=$TEMP_BUILD_DIR/source
    submodule_build=$source_build/third_party/Discord.Net
    staging=$TEMP_RELEASE_DIR
    mkdir -p "$source_build" "$submodule_build"
    tar -C "$SOURCE_DIR" --exclude=.git --exclude=bin --exclude=obj \
        --exclude=.env --exclude='.env.*' --exclude=secrets \
        --exclude=third_party/Discord.Net -cf - . | tar -C "$source_build" -xf -
    git -C "$SOURCE_DIR/third_party/Discord.Net" archive HEAD | tar -C "$submodule_build" -xf -
    chown -R rexmit-bot:rexmit-bot "$TEMP_BUILD_DIR" "$TEMP_RELEASE_DIR"

    runuser -u rexmit-bot -- env -i \
        HOME=/var/lib/rexmit \
        PATH=/opt/rexmit/tools/bin:/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin \
        NUGET_PACKAGES="$TEMP_BUILD_DIR/nuget" \
        NUGET_HTTP_CACHE_PATH="$TEMP_BUILD_DIR/http-cache" \
        DOTNET_CLI_HOME="$TEMP_BUILD_DIR/dotnet-home" \
        DOTNET_CLI_TELEMETRY_OPTOUT=1 \
        DOTNET_NOLOGO=1 \
        dotnet publish "$source_build/rexmit.csproj" \
            --configuration Release \
            --runtime linux-x64 \
            --self-contained true \
            --output "$staging" \
            -p:PublishSingleFile=true \
            -p:DebugType=None \
            -p:DebugSymbols=false

    commit=$(git -C "$SOURCE_DIR" rev-parse --short=12 HEAD 2>/dev/null || printf local)
    submodule_commit=$(git -C "$SOURCE_DIR/third_party/Discord.Net" rev-parse --short=12 HEAD)
    dirty_marker=
    if [[ -n $(git -C "$SOURCE_DIR" status --porcelain --untracked-files=all -- . ':!third_party/Discord.Net') ]]; then
        dirty_marker=' (working-tree changes included)'
    fi
    printf '%s\n' "$commit$dirty_marker; Discord.Net $submodule_commit" > "$staging/REVISION"

    local libdave_archive=$TEMP_BUILD_DIR/libdave.zip
    curl -fL --retry 3 --retry-delay 2 --retry-all-errors "$LIBDAVE_URL" -o "$libdave_archive"
    printf '%s  %s\n' "$LIBDAVE_SHA256" "$libdave_archive" | sha256sum --check --status ||
        die "Downloaded libdave failed its SHA-256 check."
    unzip -p "$libdave_archive" lib/libdave.so > "$staging/libdave.so" ||
        die "Could not extract libdave from the verified release archive."
    [[ -s $staging/libdave.so ]] || die "The libdave shared library is missing or empty."
    [[ -x $staging/rexmit ]] || die "Publish succeeded but the Linux apphost was not produced."

    local release_name
    release_name="$(date -u +%Y%m%dT%H%M%SZ)-$commit-$$"
    chown -R root:root "$staging"
    find "$staging" -type d -exec chmod 0755 {} +
    find "$staging" -type f -exec chmod 0644 {} +
    chmod 0755 "$staging/rexmit"
    mv -- "$staging" "$RELEASES_DIR/$release_name"
    TEMP_RELEASE_DIR=
    BUILT_RELEASE=$RELEASES_DIR/$release_name
    rm -rf -- "$TEMP_BUILD_DIR"
    TEMP_BUILD_DIR=
    remove_build_sdk
}

prune_releases() {
    local current=$1 previous=${2:-} directory
    while IFS= read -r directory; do
        [[ $directory == "$current" || $directory == "$previous" ]] && continue
        rm -rf -- "$directory"
    done < <(find "$RELEASES_DIR" -mindepth 1 -maxdepth 1 -type d ! -name '.staging.*' -print)
}

activate_release() {
    local release=$1 previous=
    [[ -x $release/rexmit && -s $release/libdave.so ]] || die "Release is incomplete: $release"
    if [[ -L $CURRENT_LINK ]]; then
        previous=$(readlink -f -- "$CURRENT_LINK" || true)
        [[ -n $previous && -d $previous ]] || die "The active release link is broken; refusing to replace it."
    elif [[ -e $CURRENT_LINK ]]; then
        die "The active release path exists but is not a symlink; refusing to replace it."
    fi

    if systemctl is-active --quiet rexmit.service; then
        systemctl stop rexmit.service || die "Could not stop the active Rexmit service; the release pointer was not changed."
    fi
    local current_temp=$APP_ROOT/.current.$$.$RANDOM
    ln -s -- "$release" "$current_temp"
    mv -Tf -- "$current_temp" "$CURRENT_LINK"

    if ! start_service_and_wait; then
        systemctl stop rexmit.service 2>/dev/null || true
        if [[ -n $previous && -d $previous ]]; then
            local rollback_temp=$APP_ROOT/.current.rollback.$$.$RANDOM
            ln -s -- "$previous" "$rollback_temp"
            mv -Tf -- "$rollback_temp" "$CURRENT_LINK"
            if start_service_and_wait; then
                discard_failed_build "$release"
                die "New release failed readiness; the previous release was restored and is ready."
            fi
            discard_failed_build "$release"
            die "New release failed readiness; previous release was restored but is not ready."
        fi
        rm -f -- "$CURRENT_LINK"
        discard_failed_build "$release"
        die "Initial release failed readiness; no release is active."
    fi
    if [[ -n $previous && -d $previous ]]; then
        local previous_temp=$APP_ROOT/.previous.$$.$RANDOM
        ln -s -- "$previous" "$previous_temp"
        mv -Tf -- "$previous_temp" "$PREVIOUS_LINK"
    else
        rm -f -- "$PREVIOUS_LINK"
    fi
    prune_releases "$release" "$previous"
}

rollback_release() {
    local current candidate
    current=$(readlink -f -- "$CURRENT_LINK" 2>/dev/null || true)
    [[ -n $current ]] || die "No current release is installed."
    candidate=$(readlink -f -- "$PREVIOUS_LINK" 2>/dev/null || true)
    [[ $candidate != "$current" ]] || candidate=
    [[ -n $candidate && -x $candidate/rexmit ]] || die "No previous release is available to roll back to."
    printf 'Rolling back to %s\n' "$candidate"
    activate_release "$candidate"
}

discard_failed_build() {
    local release=$1
    if [[ -n $BUILT_RELEASE && $release == "$BUILT_RELEASE" ]]; then
        rm -rf -- "$release"
        BUILT_RELEASE=
    fi
}
