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

Discovery adds no execution database, scheduler, plugin host, installation script or dynamically loaded assembly. T14 remains open until reviewed MCP/HTTP installation, invocation, upgrade/disablement and the lifecycle fixtures are qualified.

## Import and platform review

An active human with both `Integration.Manage` and `Workflow.Write` can import an exact catalog release using `POST /v1/integrations/catalog/packages/import`. Supply `packageKey`, `packageVersion` and the inspected `digest`. Optional `authSecret`/`authUsername` fields are write-only; store them through this authenticated API and never in a manifest, chat or issue. The existing tenant extension credential protector encrypts them. Repeating an import reuses the existing versioned rows and does not replace credentials or reset review. HTTP imports create one existing tool row per action; MCP imports create one existing server row with exact declared remote names.

`GET /v1/integrations/catalog/installations` returns bounded summaries and an `expectedVersion`. Use it in these commands:

- `POST /v1/integrations/catalog/installations/{id}/submit` with `expectedVersion` requests review.
- `POST /v1/integrations/catalog/installations/{id}/review` with `expectedVersion`, `approve` and optional `notes` requires the actual `PlatformAdmin` authorization policy as well as the tenant permissions. Approval leaves the installation inactive.
- `POST /v1/integrations/catalog/installations/{id}/activate` with `expectedVersion` activates an approved, unchanged installation.
- `POST /v1/integrations/catalog/installations/{id}/disable` with `expectedVersion` stops it. Disabling remains available if deployment removes the release.

Reload after a 409 conflict. Approval records the existing named reviewer and review timestamp. MCP review connects through the actual HTTP SDK, discovers at most 100 tools, and compares exact input/output schemas with the signed release. Remote tool names and annotations do not establish read authority. All remote mutations retain a High floor. HTTP review makes no business endpoint call. Every curated protocol request rechecks deployment egress; redirects and protocol responses over 1 MiB are refused. The curated client removes the host's automatic retry pipeline and disables SDK stream reconnection; the existing approval journal owns uncertain write outcomes. Package action-specific limits remain required at invocation.

Use the existing extension update path only for credential rotation while retaining the exact package contract. Rotation returns the row to Draft, deactivates it and requires fresh review. A different API-key header fails review. Endpoint/schema/auth-kind changes require a new release. Legacy submit/review/activate paths refuse packaged rows, and legacy raw MCP/HTTP tool providers exclude them. Import, review and activation create no connection grant or workflow authority; account provisioning and per-action gateway execution remain the next T14 slice.

Schema change: canonical EF CLI migration `20261115004459_AddCuratedConnectorPackageReferences` adds two nullable, bounded package-reference fields to the existing extension tables. It does not add installation, credential, execution or review tables. Apply through the normal canonical migration stream; local SQL execution is not claimed by the Windows qualification.
