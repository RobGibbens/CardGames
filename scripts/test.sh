#!/usr/bin/env bash
# Run the solution tests, including Playwright end-to-end tests.
#
# Usage:
#   ./scripts/test.sh [--headed] [dotnet test arguments]
#
#   --headed   Show Chromium windows (CARDGAMES_E2E_HEADED=1).
#   Any other arguments are forwarded to `dotnet test`.
set -euo pipefail

root="$(cd "$(dirname "$0")/.." && pwd)"
cd "$root"

dotnet_args=()
for arg in "$@"; do
  if [[ "$arg" == "--headed" ]]; then
    export CARDGAMES_E2E_HEADED=1
  else
    dotnet_args+=("$arg")
  fi
done

echo "Running dotnet tests."
if [[ "${CARDGAMES_E2E_HEADED:-}" == "1" ]]; then
  echo "Chromium will run headed."
fi

dotnet test "$root/src/CardGames.slnx" "${dotnet_args[@]}"
