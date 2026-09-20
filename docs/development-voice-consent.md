# Development voice consent

`Consent:DevelopmentVoiceDeclarationTenantIds` is an explicit, default-empty allowlist for development tenants accepting a parent's per-child declaration for the `voice` purpose. Configure only the intended development tenant. The existing generation-declaration allowlist does not enable voice consent, and the voice allowlist does not enable generation or safety consent.

This consent route still requires guardian authority, a current published notice, active core consent, a GB jurisdiction and the existing adult-parent checks. It records the actual declaration method without creating a reusable operator attestation. Withdrawal remains independent and does not remove core profile consent.

This option does not turn on an audio provider, require a paid subscription, or modify general audio endpoints. Arke Kidz enforces its own active paid-plan requirement before using its voice-creation endpoint. Other Aonik products keep their existing audio policies.

No schema migration is needed. Configure the allowlist only after deploying a build containing this option, and publish a notice that expressly covers recording and transcription. Parents must choose that purpose themselves.
