# Behavior tests

Tests encode the behavior promises in `docs/design.md`. Two layers:

## Fixture generation (runnable today)

`fixtures/generate.mjs` produces **real signed Nostr fixtures** (fixed throwaway test
keys, fixed timestamps) that the C# implementation must accept or reject:

```
cd tests/fixtures
npm install     # one-time (nostr-tools)
node generate.mjs
```

Outputs:
- `nip98.json` — NIP-98 login-event acceptance/rejection matrix (signature,
  freshness, exact `u`/`method` binding, optional payload hash).
- `allowlist.json` — NIP-51 private-list (kind 10000, NIP-44 encrypted-to-self),
  plaintext-fallback, kind 30078, newest-wins replaceable, and wrong-signature cases.

Regenerate after editing `generate.mjs`; commit the outputs so C# tests never depend
on network or signing at test time.

## C# xUnit skeleton (requires .NET 10 SDK)

`Jellyfin.Plugin.NostrAuth.Tests/` encodes the expectations and runs them against the real
implementation (all facts un-skipped): `NostrAuth.Core` directly, and the plugin's
Jellyfin-backed seams (`NostrUserProvisioner`, `NostrLoginService` +
`NostrAuthController`) behind small in-memory fakes.

```
dotnet test tests/Jellyfin.Plugin.NostrAuth.Tests
```

Test classes:
- `Nip98VerificationTests` — NIP-98 acceptance/rejection matrix.
- `AllowlistSyncTests` — decryption, plaintext fallback, newest-wins, staleness,
  fail-open/closed.
- `UserMappingTests` — npub→user provisioning, unusable passwords, provider id.
- `LoginEndpointTests` — HTTP status/reason-code contract, through the real
  controller in an in-memory ASP.NET Core host.
