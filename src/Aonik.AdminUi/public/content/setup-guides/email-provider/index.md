# Configuring your email provider

Transactional email keeps operators and customers informed about billing, onboarding, and payment events.

## What you need

- SMTP or API credentials from your email provider.
- A verified sender domain.
- A dedicated notifications address.

## Recommended setup

1. Verify the sending domain and add SPF/DKIM records.
2. Configure bounce handling and suppression lists.
3. Test notification templates in a staging tenant.

## Operational notes

- Route sensitive notices through a monitored mailbox.
- Track deliverability for high-value workflows.
