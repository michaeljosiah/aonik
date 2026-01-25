# Generating API keys

API keys enable secure programmatic access to the platform.

## Recommended approach

- Create a dedicated key for each environment.
- Assign scopes based on least privilege.
- Rotate keys on a regular schedule.

## Usage guidance

1. Store keys in a secret manager.
2. Never embed keys in client-side code.
3. Revoke keys immediately if compromised.
