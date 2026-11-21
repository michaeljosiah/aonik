# Gmail development OAuth setup

This is the prerequisite for the consenting-account qualification in epic #438. A Google account connection in another product does not provision AONIK's connector. Use a dedicated development OAuth client and the account designated by the tenant owner. Keep the test mailbox address and credentials out of committed evidence.

## Google client

Use the AONIK platform's development project. The current project is **Aonik** (`gen-lang-client-0482317472`); verify the selected project name and ID before saving configuration or creating credentials. Product projects such as Payabo have separate ownership and are not the default home for platform connectors.

In the intended Google Cloud project, enable Gmail API and configure the Google Auth Platform application for testing. Add the designated mailbox as an explicit test user. Create a **Web application** client named **AONIK Gmail Automation — Development**, separate from AONIK sign-in clients. Use the AI-powered agent option for the dedicated automation client. AONIK uses the Gmail REST API. Google requires the option for its Gmail MCP endpoint and offers it for agentic Web application clients. See [Google's credential setup](https://developers.google.com/workspace/guides/create-credentials), [OAuth consent setup](https://developers.google.com/workspace/guides/configure-oauth-consent) and [Gmail release notes](https://developers.google.com/workspace/gmail/release-notes).

Register the exact API callback:

```text
https://aonik-dev-api.delightfulisland-9fd7c1e7.uksouth.azurecontainerapps.io/integrations/gmail/oauth/callback
```

The server handles authorization-code exchange with PKCE. No browser JavaScript client secret is used. Store the confidential client's secret directly in the deployment secret store, or transfer the downloaded credentials privately to the authorized host configuration. Never paste it into chat, workflow JSON, a PR, or browser environment variables. Client creation and mailbox consent require the accountable owner's review.

Capture the new credential privately before leaving its creation view or changing another secret. The console can remove its download controls after another secret operation. During rotation, verify the replacement is stored before disabling the previous secret. Keep temporary transfer files outside the repository with access restricted to their owner, then remove them after storage succeeds. Accessibility copy-button labels can contain the secret even when the displayed value is masked; exclude those labels from setup evidence.

## Host configuration

Configure these values for API **and Worker**, preserving their shared persistent data-protection key ring and private storage:

| Configuration key | Value |
| --- | --- |
| `Integrations__Gmail__OAuth__ClientId` | Created development client ID |
| `Integrations__Gmail__OAuth__ClientSecret` | Deployment secret reference |
| `Integrations__Gmail__OAuth__RedirectUri` | Exact API callback above |
| `Integrations__Gmail__OAuth__ReviewReturnUri` | `https://aonik-dev-adminui.delightfulisland-9fd7c1e7.uksouth.azurecontainerapps.io/settings/my-connections` |

For Container Apps deployments, set the GitHub environment variables `GMAIL_OAUTH_CLIENT_ID` and `GMAIL_OAUTH_CLIENT_SECRET_URI`. The deployment workflows resolve them to the `gmailOAuthClientId` and `gmailOAuthClientSecretUri` ACA parameters. Use the development Key Vault secret URI. The stack supplies the API callback and Admin return URI to both hosts and uses their existing managed identities to read the secret. The same stack configures shared Blob Storage for the hosts. Confirm all four Gmail variable names exist on both active revisions and the Worker runs before the rehearsal.

The callback uses its protected owner/tenant state and HttpOnly cookie. An anonymous callback or tenant header cannot choose the account's tenant. After callback, the owner signs in to review the returned request and explicitly confirms the provider identity. A callback alone does not grant use.

## Scopes and rehearsal

Start with `openid`, `email` and `https://www.googleapis.com/auth/gmail.readonly` for search, messages and attachments. Request the exact actions through AONIK's connection-request flow. Add compose/send/modify consent only when qualifying the corresponding actions; existing action grants continue to limit use even when Google consent is broader. Permanent deletion requires its separate global-and-tenant switch, full-mail scope and exact High proposal. Google's [Gmail scope reference](https://developers.google.com/workspace/gmail/api/auth/scopes) describes each scope; production verification is separate from a testing-client rehearsal.

Use synthetic subjects with a unique exact prefix, synthetic invoices, and only the designated mailbox/recipients. Pin the actual connection and explicit workflow/automation grants. Review activation and each Medium/High action separately through AONIK. Keep sends, draft versions, recipients and attachments fixed to their reviewed evidence; never infer permission from an email's contents. Preserve uncertain operations and reconcile them through the recorded read-only recovery path.

Confirm the feature branch, generated migrations, OCR/scanner/private-storage prerequisites and tenant switches are deployed before qualification. Record sanitized IDs, hashes, action outcomes, consent scope names and run/approval references. Do not record codes, tokens, secrets, raw message bodies or the mailbox address. Record the client setup, consent and live action qualification separately after each succeeds.
