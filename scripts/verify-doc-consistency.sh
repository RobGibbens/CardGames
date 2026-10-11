#!/usr/bin/env bash
# Fails when onboarding docs instruct a manual PokerGameRulesRegistry edit.
set -euo pipefail

root="$(cd "$(dirname "$0")/.." && pwd)"
fail=0

check_absent() {
  local file="$1"
  local pattern="$2"
  local reason="$3"
  if grep -nE "$pattern" "$file" >/dev/null; then
    echo "Doc drift in ${file#$root/}: $reason" >&2
    grep -nE "$pattern" "$file" >&2 || true
    fail=1
  fi
}

adding="$root/docs/ADDING_NEW_GAMES.md"
architecture="$root/docs/ARCHITECTURE.md"
readme="$root/README.md"

# Manual dictionary registration snippet (historical docs told readers to edit this).
dict_pattern='FrozenDictionary<string, Func<GameRules>>|RulesByGameTypeCode'
check_absent "$adding" "$dict_pattern" "manual rules-dictionary pattern"
check_absent "$architecture" "$dict_pattern" "manual rules-dictionary pattern"

# "Register in the API" together with an instruction to edit the registry source file.
register_api='Register in the API'
registry_edit='PokerGameRulesRegistry\.cs'
for file in "$adding" "$architecture"; do
  if grep -nF "$register_api" "$file" >/dev/null && grep -nE "$registry_edit" "$file" >/dev/null; then
    echo "Doc drift in ${file#$root/}: tells readers to register in the API by editing PokerGameRulesRegistry.cs" >&2
    fail=1
  fi
done

check_absent "$readme" 'Register the game in `PokerGameRulesRegistry`' "README still says to register the game in PokerGameRulesRegistry"

if [[ "$fail" -ne 0 ]]; then
  exit 1
fi

echo "Doc consistency checks passed."
