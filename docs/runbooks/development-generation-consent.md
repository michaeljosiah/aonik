# Development generation declarations

`Consent:DevelopmentGenerationDeclarationTenantIds` is empty by default. It is a separate tenant opt-in from core profile enrolment and is intended for fictional-data development only.

An existing guardian can POST to `/consent/wards/{childPartyId}/purposes` with the purpose, current termsVersion, GB jurisdiction and parentalResponsibilityDeclared true. Only generation-disclosure and safety-classification use this route. Other purposes keep the existing verification process. Known minors, stale notices, absent core consent, unknown guardians and tenants without this opt-in are refused.

The saved method is parental-declaration, with the terms version as its reference. Granting the same version again is idempotent. DELETE of the individual purpose withdraws it without withdrawing service-core. No reusable attestation is recorded. Ordinary GrantAsync callers still cannot supply parental-declaration as a verification method.

Kidz publishes the known profile-notice upgrade with affected purposes restricted to the two AI purposes, preserving existing core grants. The notice names Aonik and OpenAI; a different downstream provider needs a reviewed notice update. Permission alone does not configure a classifier, generate content or reserve usage.

This development opt-in is not a production guardian-assurance assessment. Production use requires reviewing the actual data, processing risks and verification route.
