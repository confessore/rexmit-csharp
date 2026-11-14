#!/usr/bin/env bash
set -Eeuo pipefail

repo_root=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/../.." && pwd)
SCRIPT_ROOT=$repo_root
SCRIPT_PATH=$repo_root/scripts/rexmit
SCRIPT_DIR=$repo_root/scripts
source "$repo_root/scripts/lib/common.sh"
source "$repo_root/scripts/lib/service.sh"
source "$repo_root/scripts/lib/dependencies.sh"

test_root=$(mktemp -d)
trap 'rm -rf -- "$test_root"' EXIT
export PATH="$test_root:$PATH"

cat > "$test_root/systemctl" <<'STUB'
#!/usr/bin/env bash
case ${1:-} in
    is-active) [[ ${STUB_ACTIVE:-1} == 1 ]] ;;
    show) printf '%s\n' "${STUB_MAIN_PID:-42}" ;;
    *) exit 0 ;;
esac
STUB
cat > "$test_root/journalctl" <<'STUB'
#!/usr/bin/env bash
if [[ ${1:-} == -u && ${STUB_READY_PID:-42} != none ]]; then
    printf 'REXMIT_READY pid=%s\n' "$STUB_READY_PID"
fi
STUB
cat > "$test_root/sleep" <<'STUB'
#!/usr/bin/env bash
exit 0
STUB
chmod +x "$test_root/systemctl" "$test_root/journalctl" "$test_root/sleep"

STUB_MAIN_PID=42 STUB_READY_PID=42 wait_for_ready 3
if STUB_MAIN_PID=42 STUB_READY_PID=41 wait_for_ready 0 2>/dev/null; then
    echo 'A readiness marker from a different process was accepted.' >&2
    exit 1
fi
if STUB_ACTIVE=0 STUB_MAIN_PID=42 STUB_READY_PID=42 wait_for_ready 3 2>/dev/null; then
    echo 'An inactive service was accepted as ready.' >&2
    exit 1
fi

validate_package_removal_plan $'Remv ffmpeg [7.0]\nRemv libopus-dev [1.5]' ffmpeg libopus-dev
if validate_package_removal_plan $'Remv ffmpeg [7.0]\nRemv unrelated [1.0]' ffmpeg 2>/dev/null; then
    echo 'An APT plan that removes an unowned package was accepted.' >&2
    exit 1
fi

echo 'Management readiness and dependency-removal checks passed.'
