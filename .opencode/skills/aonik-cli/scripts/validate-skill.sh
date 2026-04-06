#!/usr/bin/env bash
set -euo pipefail

skill_dir="${1:-$(cd "$(dirname "$0")/.." && pwd)}"
skill_file="$skill_dir/SKILL.md"
dir_name="$(basename "$skill_dir")"

if [[ ! -f "$skill_file" ]]; then
  printf 'Error: %s not found.\n' "$skill_file" >&2
  exit 1
fi

name_line="$(grep -E '^name:[[:space:]]+' "$skill_file" | head -n 1 || true)"
description_line="$(grep -E '^description:[[:space:]]+' "$skill_file" | head -n 1 || true)"

if [[ -z "$name_line" ]]; then
  printf 'Error: SKILL.md is missing a name field.\n' >&2
  exit 1
fi

if [[ -z "$description_line" ]]; then
  printf 'Error: SKILL.md is missing a description field.\n' >&2
  exit 1
fi

skill_name="${name_line#name: }"

if [[ "$skill_name" != "$dir_name" ]]; then
  printf 'Error: name field "%s" does not match directory "%s".\n' "$skill_name" "$dir_name" >&2
  exit 1
fi

if ! [[ "$skill_name" =~ ^[a-z0-9]+(-[a-z0-9]+)*$ ]]; then
  printf 'Error: skill name "%s" is not spec-compliant.\n' "$skill_name" >&2
  exit 1
fi

if command -v skills-ref >/dev/null 2>&1; then
  printf 'Running skills-ref validation...\n'
  skills-ref validate "$skill_dir"
else
  printf 'skills-ref not installed; skipping external validation.\n'
fi

printf 'Skill structure looks valid: %s\n' "$skill_dir"
