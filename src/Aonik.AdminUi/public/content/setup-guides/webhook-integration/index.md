# Integrating webhooks

Webhooks deliver real-time payment, settlement, and status events to your systems.

## Before you start

- Identify your receiving endpoint and authentication method.
- Ensure the endpoint is reachable from the Aonik platform.

## Setup steps

1. Register your endpoint URL in the Admin UI.
2. Configure signature validation or token authentication.
3. Subscribe to the event types you need.

## Best practices

- Respond with HTTP 200 within 2 seconds.
- Log payloads for reconciliation and support.
