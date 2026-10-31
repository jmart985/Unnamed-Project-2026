#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"

godot_bin="${GODOT_BIN:-godot}"
log_dir="${GODOT_VALIDATION_LOG_DIR:-${RUNNER_TEMP:-${TMPDIR:-/tmp}}}"
mkdir -p "$log_dir"

# Keep this list in sync with Main.cs. The parity check below fails when a new
# command-line scenario is registered without adding it to CI.
scenarios=(
  "--validate-basic-settlement|BASIC SETTLEMENT VALIDATION PASSED"
  "--validate-construction|CONSTRUCTION VALIDATION PASSED"
  "--validate-farm|FARM VALIDATION PASSED"
  "--validate-workbench|WORKBENCH VALIDATION PASSED"
  "--validate-social-feedback|SOCIAL FEEDBACK VALIDATION PASSED"
  "--validate-follow-spacing|FOLLOW SPACING VALIDATION PASSED"
  "--validate-squad|SQUAD VALIDATION PASSED"
  "--validate-first-game|FIRST GAME VALIDATION PASSED"
  "--validate-session-profiles|SESSION PROFILE VALIDATION PASSED"
  "--validate-schedules|SCHEDULE VALIDATION PASSED"
  "--validate-schedule-integration|SCHEDULE INTEGRATION PASSED"
  "--validate-menus|MENU VALIDATION PASSED"
  "--validate-selection|SELECTION VALIDATION PASSED"
  "--validate-gameplay-actions|GAMEPLAY ACTIONS VALIDATION PASSED"
  "--validate-individual-orders|INDIVIDUAL ORDER VALIDATION PASSED"
  "--validate-interaction-anchors|INTERACTION ANCHOR VALIDATION PASSED"
  "--validate-wild-territories|WILD TERRITORY VALIDATION PASSED"
  "--validate-wild-prey-threat|WILD PREY THREAT VALIDATION PASSED"
  "--validate-wolf-hunting|WOLF HUNTING VALIDATION PASSED"
  "--validate-carcass-decay|CARCASS DECAY VALIDATION PASSED"
  "--validate-far-population-transitions|FAR POPULATION TRANSITIONS VALIDATION PASSED"
  "--validate-starter-wildlife-proximity|STARTER WILDLIFE PROXIMITY VALIDATION PASSED"
  "--validate-npc-locomotion|NPC LOCOMOTION VALIDATION PASSED"
  "--validate-species-presentations|SPECIES PRESENTATION VALIDATION PASSED"
  "--validate-player-workwear|PLAYER WORKWEAR VALIDATION PASSED"
  "--validate-character-lighting|CHARACTER LIGHTING VALIDATION PASSED"
)

mapfile -t registered_flags < <(
  grep -oE -- '--validate-[a-z0-9-]+' game/scripts/Main.cs | sort -u
)
mapfile -t ci_flags < <(
  printf '%s\n' "${scenarios[@]}" | cut -d '|' -f 1 | sort -u
)

if ! diff -u <(printf '%s\n' "${registered_flags[@]}") <(printf '%s\n' "${ci_flags[@]}"); then
  echo "Godot validation manifest does not match the scenarios registered in game/scripts/Main.cs." >&2
  exit 1
fi

failures=()
for scenario in "${scenarios[@]}"; do
  IFS='|' read -r flag pass_marker <<< "$scenario"
  log_file="$log_dir/${flag#--}.log"
  echo "Running $flag"
  if ! "$godot_bin" --headless --path game -- "$flag" 2>&1 | tee "$log_file"; then
    failures+=("$flag exited unsuccessfully")
    continue
  fi
  if ! grep -Fq "$pass_marker" "$log_file"; then
    failures+=("$flag did not emit its expected pass marker: $pass_marker")
  fi
done

if ((${#failures[@]} > 0)); then
  printf 'Godot validation failures:\n' >&2
  printf ' - %s\n' "${failures[@]}" >&2
  exit 1
fi

echo "All ${#scenarios[@]} registered Godot validation scenarios passed."
