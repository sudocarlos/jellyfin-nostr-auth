# Design & Security

This page summarizes the design rationale. The complete specification —
including diagrams and the behavior-test matrix — lives in
[`docs/design.md`](https://github.com/sudocarlos/jellyfin-nostr-auth/blob/master/docs/design.md)
in the repository.

## Key behaviors

- **Authorization source is the private list, not the owner identity.** The
  allowlist event is authored by a dedicated list keypair whose only purpose
  is this server's access list. The owner's identity npub is shown for context
  only and is never trusted.
- **Every list event is signature-verified.** The NIP-01 event id is recomputed
  and the BIP-340 Schnorr signature checked against the list npub. Relay trust
  plays no part in authenticity, only availability.
- **Newest created_at wins.** The allowlist is a replaceable event with no
  push or revocation semantics; the plugin defines its own staleness policy:
  cache age beyond *max list age* fails new logins closed, while the cached
  list stays honored during relay outages (unless *fail closed* is set).
- **Login is a NIP-98 event** (kind 27235). Verification requires a valid
  Schnorr signature, a 60-second freshness window in both directions, exact
  `u` and `method` tag equality, and an optional `payload` tag equal to the
  sha256 of the raw request body.

## Interop landmine: the NIP-98 payload tag

nostr-tools' `nip98.hashPayload` hashes `JSON.stringify(payload)` — for a
string body that double-encodes it and diverges from the NIP-98 spec, which
hashes the raw body bytes. Since client and server would compute different
hashes, the plugin's login page computes the payload tag itself rather than
calling that helper. Both sides are pinned to the same nostr-tools version
(the server's test fixtures are signed with it), so acceptance is verified by
fixtures, not by convention.

## User provisioning

On first successful login the plugin creates a Jellyfin user named after the
bech32 npub via `IUserManager.CreateUserAsync`, assigns an unusable random
password, and sets `AuthenticationProviderId` to
`Jellyfin.Plugin.NostrAuth` — which deliberately names no password provider,
so password authentication fails closed for these users even if the random
password were somehow known. The npub → user mapping is stored in plugin
configuration and re-resolved on later logins; a deleted user is
re-provisioned under the same npub-derived name.

The session itself is minted through `ISessionManager.AuthenticateDirect`
(Jellyfin 12.x single-argument overload), which enforces device access and
session caps and issues the access token — the same `AuthenticationResult`
shape as the built-in password login.

## Anonymous endpoints on Jellyfin 12

Jellyfin's authorization setup sets only `DefaultPolicy` (applied to endpoints
marked `[Authorize]`) and no fallback policy, so an unmarked plugin controller
action is anonymous. The login endpoint (`POST /NostrAuth/Login`) is anonymous
by design — the NIP-98 signature in the Authorization header is the only
authenticator — and the admin endpoints (`Status`, `GenerateKeypair`) are
locked to `RequiresElevation`.

## Trust boundaries

Explicitly not trusted for authorization: NIP-05 identifiers, NIP-46 client
metadata (`name`/`url`/`image` — unauthenticated per spec), relay-supplied
metadata, and the owner npub. The list nsec is the only key material held
server-side; its compromise reveals and rewrites this one server's access
list, and rotation is a new keypair away.
