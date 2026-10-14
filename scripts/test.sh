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

export CARDGAMES_E2E=1

dotnet_args=()
for arg in "$@"; do
  if [[ "$arg" == "--headed" ]]; then
    export CARDGAMES_E2E_HEADED=1
  else
    dotnet_args+=("$arg")
  fi
done

echo "Running dotnet tests with Playwright E2E enabled (CARDGAMES_E2E=1)."
if [[ "${CARDGAMES_E2E_HEADED:-}" == "1" ]]; then
  echo "Chromium will run headed."
fi

dotnet test "$root/src/CardGames.slnx" "${dotnet_args[@]}"
