# Curated connector descriptor catalog

Spec 099 T14 extends the existing connector gateway and tenant MCP/HTTP administration. The discovery slice verifies and exposes passive descriptor artifacts. Installation/review, account grants and runtime execution have separate authority; discovering or downloading an artifact does not authorize any of them.

## Deployment trust

The catalog is empty by default. Configure `Agents:ConnectorCatalog` only through deployment-owned API/Worker configuration:

```json
{
  "Agents": {
    "ConnectorCatalog": {
      "ArtifactDirectory": "/etc/aonik/connector-catalog",
      "ArtifactFiles": ["gmail-1.0.0.json"],
      "TrustedPublishers": [
        {
          "PublisherId": "aonik",
          "KeyId": "release-key-1",
          "PublicKeyPem": "<RSA public key PEM>"
        }
      ]
    }
  }
}
```

Use an absolute artifact directory appropriate to the host and a filename list within that directory. Files are bounded to 1 MiB; the catalog accepts at most 100 artifacts. Tenant/user settings cannot change paths or trusted publishers. Keep publisher private keys outside the application and repository. No synthetic test key is trusted by default. Deploy the same artifacts and trust configuration to API and Worker.

## Artifact contract

An envelope contains `schemaVersion: 1`, `publisherId`, `keyId`, `payloadBase64`, `sha256` and `signatureBase64`. The payload is the exact UTF-8 manifest bytes. `sha256` is their SHA-256 hex digest. The publisher signs those same bytes with RSA-PSS/SHA-256 using a 2048–4096-bit key. Whitespace changes to the payload require a fresh signature; envelope formatting does not change the signed payload. Public key selection comes from deployment trust.

The manifest declares publisher/package/version/provider identity, adapter kind (`Native`, `Mcp`, `Http`), host contract range, authentication kind, resource limits and exact action versions, schemas, minimum tiers, scopes and endpoints. Host contract version is currently 1. Package versions use three numeric components. Input/output descriptors and arguments remain passive data. Unknown fields, duplicate fields, casing aliases, scripts, unknown adapters, invalid limits and incompatible host versions are rejected.

Native manifests must agree with deployed action schemas, code-owned approval tiers and scope alternatives. Native Gmail retains its real OAuth provider and current consent/grant policy. Reviewed MCP/HTTP packages use the existing supported tenant extension credential modes; OAuth-dependent or scope-bearing remote packages require a native OAuth adapter rather than fabricated consent. Endpoint egress checks remain mandatory at review and invocation.

## Discovery and retained versions

Authorized active tenant readers with `Integration.Read` can use:

- `GET /v1/integrations/catalog/packages` for bounded release summaries.
- `GET /v1/integrations/catalog/packages/{packageKey}/{version}` for the verified manifest.
- `GET /v1/integrations/catalog/packages/{packageKey}/{version}/artifact` for the exact original artifact download.
- Aonik's `aonik_list_connector_packages` and `aonik_get_connector_package` read tools.

Retain old releases while published bindings/runs need their pins. Schema, endpoint, authentication, limits or approval changes under the same action version are rejected across loaded releases. Publish a new action version for changed behavior. Removing a release must explicitly block/drain its consumers; rollback cannot restore revoked account access.

Discovery uses the existing workflow runtime and tenant extension tables. Tenant Integrations exposes the catalog, review commands, account registration and upgrade impact.

## Import and platform review

An active human with both `Integration.Manage` and `Workflow.Write` can import an exact catalog release using `POST /v1/integrations/catalog/packages/import`. Supply `packageKey`, `packageVersion` and the inspected `digest`. Optional `authSecret`/`authUsername` fields are write-only; store them through this authenticated API and never in a manifest, chat or issue. The existing tenant extension credential protector encrypts them. Repeating an import reuses the existing versioned rows and does not replace credentials or reset review. HTTP imports create one existing tool row per action; MCP imports create one existing server row with exact declared remote names.

`GET /v1/integrations/catalog/installations` returns bounded summaries and an `expectedVersion`. Use it in these commands:

- `POST /v1/integrations/catalog/installations/{id}/submit` with `expectedVersion` requests review.
- `POST /v1/integrations/catalog/installations/{id}/review` with `expectedVersion`, `approve` and optional `notes` requires the actual `PlatformAdmin` authorization policy as well as the tenant permissions. Approval leaves the installation inactive.
- `POST /v1/integrations/catalog/installations/{id}/activate` with `expectedVersion` activates an approved, unchanged installation.
- `POST /v1/integrations/catalog/installations/{id}/disable` with `expectedVersion` stops it. Disabling remains available if deployment removes the release.

Reload after a 409 conflict. Approval records the existing named reviewer and review timestamp. MCP review connects through the actual HTTP SDK, discovers at most 100 tools, and compares exact input/output schemas with the signed release. Remote tool names and annotations do not establish read authority. All remote mutations retain a High floor. HTTP review makes no business endpoint call. Every curated protocol request rechecks deployment egress; redirects and protocol responses over 1 MiB are refused. The curated client removes the host's automatic retry pipeline and disables SDK stream reconnection; the existing approval journal owns uncertain write outcomes. Package action-specific limits remain required at invocation.

Rotate a credential with `POST /v1/integrations/catalog/installations/{id}/credential`. Supply the current `expectedVersion` and write-only credential fields. Rotation returns the row to Draft and deactivates it. Submit, review and activate again before continuing. A changed credential version invalidates earlier approvals. Endpoint, schema and authentication-contract changes require a new signed release. Use the catalog commands for packaged rows.

## Accounts and action use

1. Enable the curated connector policy explicitly for the tenant. Review the allowed action versions and enable workflow connections if needed. Global and tenant policy both constrain use. Personal defaults continue to apply to eligible Gmail user connections.
2. After activation, register the exact reviewed installation through Tenant Integrations, or call `POST /v1/integrations/connectors/{connectorKey}/service-accounts` with `adapterKind`, `installationId` and `displayName`. This creates or returns the tenant-owned account record. A credential remains encrypted on its existing tenant extension row.
3. Approve a connection request against that account or issue an explicit grant for each actor and action version. Add a separate workflow grant and an automation-identity grant for unattended execution. Agent actions and Workflow Studio use the same connector authorization. The native Gmail OAuth and account-consent flow is unchanged.
4. Select the account and grant in Workflow Studio. The saved action includes the connector release, artifact digest and action contract hash. Deployed signed native descriptors are checked against the code-owned action and scope contracts. Older published native bindings retain their previous authorization behavior.

Curated HTTP uses typed JSON over a fixed reviewed endpoint template. Curated MCP uses Streamable HTTP and requires structured JSON output. Input and output must satisfy the signed bounded schemas. The runtime rechecks the current account, grant, endpoint and tool schema. A non-read remote action enters the existing durable High proposal and journal, with the account, artifact, credential version and exact arguments in the preview. A reply lost after dispatch requires review of the held operation. Retry a changed write only as a newly reviewed action after resolving the original outcome.

## Upgrade and disablement

Use the installation impact view or `GET /v1/integrations/catalog/installations/{id}/impact?targetVersion=...` before updating. It lists the pinned workflow versions and open runs it finds, and indicates when the bounded search is truncated. Compare the target action and version, scopes, approval tier and contract hash.

Import, review, activate and register the new release independently. Rebind and publish new workflow versions explicitly. Keep the prior release and reviewed installation available while its runs drain. To stop the prior integration, disable it or revoke its grants. A pinned step with missing authority pauses for connection recovery. Restore an eligible reviewed release and grant, then resume through the existing run controls. A held write may need separate reconciliation before run recovery.

Schema change: canonical EF CLI migration `20261115004459_AddCuratedConnectorPackageReferences` adds two nullable, bounded package-reference fields to the existing extension tables. It does not add installation, credential, execution or review tables. Apply through the normal canonical migration stream. [CI run 1391](https://github.com/michaeljosiah/aonik/actions/runs/43675234907) verified the canonical model and SQL checks.
