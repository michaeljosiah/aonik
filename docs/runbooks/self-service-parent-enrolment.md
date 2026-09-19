# Self-service parent enrolment

Kidz requested enrolment without an operator reviewing a form. `POST /consent/wards` now accepts `parentalResponsibilityDeclared: true` as a request-scoped declaration. The authenticated endpoint derives the guardian party from the current user; callers cannot name another guardian.

This route is enabled only for tenants listed in `Consent:ParentalDeclarationTenantIds`. It requires GB, current published terms, an active individual/person not known to be a minor, and only the core-service purpose. The audit and initial grant use `parental-declaration`, not `signed-form`, `government-id`, or `payment-instrument`. No reusable attestation is created. Requests without the declaration retain the previous verification flow. Adding another guardian and granting additional purposes retain their existing checks.

The initial deployment is limited to Kidz's fictional-data development environment. This is a product policy extension to Spec 095, not a claim that the declaration proves age or parental responsibility. It does not change the general jurisdiction method list. Before using this policy for real child data, assess the processing risks and appropriate automated assurance in the product DPIA. [ICO guidance](https://ico.org.uk/for-organisations/uk-gdpr-guidance-and-resources/childrens-information/children-and-the-uk-gdpr/how-do-the-lawful-bases-apply-to-children-s-personal-information/) requires reasonable efforts to verify parental consent where Article 8 applies, rather than prescribing operator approval.

To disable new declaration enrolments, remove the tenant from the setting and restart the API. Existing grants remain subject to normal withdrawal and terms supersession. No schema migration is required.
