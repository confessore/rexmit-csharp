#!/usr/bin/env bash
set -Eeuo pipefail

repo_root=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)
cd "$repo_root"

DOTNET=${DOTNET:-dotnet}
if ! command -v "$DOTNET" >/dev/null 2>&1 && command -v dotnet.exe >/dev/null 2>&1; then
    DOTNET=dotnet.exe
fi

mapfile -d '' csharp_files < <(
    find . \
        -path './third_party' -prune -o \
        -path '*/bin' -prune -o \
        -path '*/obj' -prune -o \
        -name '*.cs' -print0
)

"$DOTNET" build rexmit.sln
"$DOTNET" test rexmit.sln --no-build
"$DOTNET" format rexmit.sln --verify-no-changes --include "${csharp_files[@]}"
bash -n scripts/rexmit scripts/check.sh scripts/lib/*.sh tests/shell/manager.sh
bash tests/shell/manager.sh
