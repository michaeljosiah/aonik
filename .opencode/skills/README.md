# Local Skills

This directory contains local skills bundled with this repository.

## Skills

- `aonik-cli`: use the local AONIK CLI for agent-driven command-line interaction with AONIK systems
- `test-admin-ui`: launch the Admin UI, authenticate with Auth0, and test pages through Playwright

## File Layout

- skill instructions live in `<skill-name>/SKILL.md`
- optional reference material lives in `<skill-name>/references/`
- optional helper scripts live in `<skill-name>/scripts/`

## Validation

Validate the CLI skill with:

```bash
bash .opencode/skills/aonik-cli/scripts/validate-skill.sh
```

If `skills-ref` is installed, the script will run the official Agent Skills validator as well.
