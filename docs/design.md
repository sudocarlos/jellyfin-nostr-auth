Status: draft

# Jellyfin Nostr Auth plugin

## Overview

A Jellyfin server plugin (targeting Jellyfin 12.x, .NET 10) that replaces username/password
login with Nostr authentication for the web client. The server owner authorizes access by
publishing a **private Nostr allowlist** under a dedicated list keypair; viewers log in by
proving control of a Nostr key that appears in that list.

Login proof is a NIP-98 signed event (kind 27235) produced by whichever signer the user
prefers: a NIP-07 browser extension (Alby, nos2x), a NIP-46 remote signer / nsec bunker
(Amber, nsec.app, nsecBunker), or an Android signer (Amber / NIP-55). All of these produce
the same artifact — a signature over a NIP-98 event — so the server has one verification
path regardless of login method.

The plugin never holds the owner's identity nsec. Authorization key material is a dedicated
"list keypair" whose only purpose is the allowlist; compromise of it reveals and rewrites
this one server's access list, nothing else.

### Terminology

- **owner npub** — the server owner's identity key. Shown in the dashboard for context;
  never trusted for authorization; never given to the plugin.
- **list npub / list nsec** — dedicated keypair that authors the allowlist. The plugin
  stores the list nsec in plugin configuration to decrypt and verify the list.
- **allowlist event** — a private NIP-51 list (kind 10000) authored by the list npub whose
  private (NIP-44-encrypted-to-self) content contains `p`-tagged npubs authorized to use
  the server.

## Key behaviors

- **Authorization source is the owner's private list, not the owner identity.**
  The allowlist event is a kind-10000 replaceable event authored by the list npub. Private
  list items are stored in the event's `.content` as a NIP-51 stringified tags-array,
  NIP-44-encrypted with the author's own key pair (encrypt-to-self), per NIP-51. Because
  the list nsec is both the decryption key and the publish key, a leaked list nsec can
  rewrite the allowlist — accepted risk: whoever can read plugin configuration already
  controls the server, and the key is trivially rotated (new keypair → republish → update
  config). Rationale for separation from the owner npub: the plugin holds no
  identity-grade key material, and NIP-51 self-encryption means no custom
  encryption-to-arbitrary-key tooling is needed.

- **Two event kinds supported for the allowlist.** Primary: kind 10000 (authorable in
  every mainstream client as a "private list"). Advanced: kind 30078 with
  `d="jellyfin-allowlist"` (NIP-78 app-specific data; cleaner semantics, no client UI).
  Configured via plugin settings.

- **Decrypt-or-fallback reading of the list.** If `.content` is NIP-44 ciphertext
  (version byte `0x02`), decrypt it with the list nsec and read the tags-array; if it is
  plaintext, read `p`-tags from the event tags directly (some clients publish private
  lists in plaintext). If the resulting list is plaintext and the configured mode is
  "private", the dashboard shows a warning that the list is effectively public.

- **Every list event is signature-verified against the list npub.** NIP-01 event id is
  recomputed (sha256 over canonical serialization) and the BIP-340 Schnorr signature is
  verified. A valid signature proves the list npub authored the event; relay trust plays
  no part in authenticity, only in availability.

- **Replaceable-event selection and staleness.** The allowlist event is replaceable: when
  multiple versions are seen, the one with the highest `created_at` (signature-valid)
  wins. The plugin re-fetches on a poll interval (default 10 minutes, configurable) and
  re-validates the allowlist at session creation. Between polls it serves the cached
  allowlist as long as the event age is below a configurable max-age (default 24h); past
  max-age, new logins fail closed. Rationale: replaceable events have no push or
  revocation semantics; the plugin must define its own staleness policy.

- **Relay outage does not fail closed by default.** If all configured relays are
  unreachable, the plugin keeps honoring the last-known allowlist (within max-age).
  A `failClosed` config option reverses this for owners who prefer availability-risk over
  staleness-risk.

- **Login is a NIP-98 event.** `POST /NostrAuth/Login` accepts
  `Authorization: Nostr <base64(kind-27235 event)>`. Verification requires: valid
  Schnorr signature; `kind == 27235`; `created_at` within 60 seconds; `u` tag exactly
  equal to the request's absolute URL including query; `method` tag exactly equal to the
  HTTP method; optional `payload` tag equal to sha256 hex of the request body. Any
  signer that can sign events — NIP-07 extension, NIP-46 bunker (via `bunker://` token or
  `nostrconnect://` QR), NIP-55/Amber — can produce this header; the server has a single
  verification path. Rationale: NIP-98 is the only Nostr spec for server-side HTTP auth
  and has a C# reference implementation linked from the spec itself.

- **NIP-46 is only involved at login time.** The bunker (if used) is contacted to obtain
  the user pubkey and sign the NIP-98 event. Issued Jellyfin sessions never require the
  bunker to be online afterwards. Clients must enforce their own connection timeouts
  (the NIP-46 spec defines none); the login page uses a 60-second budget for bunker
  responses before surfacing a retryable error.

- **User provisioning: npub → Jellyfin user, auto-created.** On first successful login,
  the plugin creates a Jellyfin user via `IUserManager.CreateUserAsync`, sets
  `AuthenticationProviderId` to the plugin's type name, and assigns an unusable random
  password (SSO-plugin pattern). The npub→user mapping is stored in plugin configuration
  and re-resolved on later logins. Removal of the npub from the allowlist prevents new
  sessions but does not revoke live sessions — Jellyfin's own session lifetime governs
  those. Rationale: matches existing external-identity plugin precedent (LDAP, SSO) and
  avoids surprising mid-playback cut-offs; owners can force-kill sessions from the
  dashboard.

- **The web login form is extended, not replaced.** jellyfin-web has no plugin hook to
  replace the login page; the plugin provides a Nostr login button to be injected via
  Dashboard → General → Branding ("Login disclaimer" + custom CSS), pointing at the
  plugin's login page. Native clients (TV/mobile apps) continue to require
  username/password and are out of scope for v1.

- **Explicitly not trusted for authorization:** NIP-05 identifiers, NIP-46 client
  metadata (`name`/`url`/`image` — unauthenticated per spec), relay-supplied metadata,
  and the owner npub. Only verified signatures over the allowlist event and the NIP-98
  login event matter.

## External contracts

### Plugin configuration (dashboard)

| Setting | Type | Default | Meaning |
|---|---|---|---|
| `listNpub` | bech32 npub | — (required) | Identity that authors the allowlist |
| `listNsec` | bech32 nsec | — (required) | Stored server-side; decrypts the list content |
| `relays` | string[] | — (required) | Relays used to fetch the list |
| `listKind` | enum | `10000` | `10000` or `30078` |
| `pollIntervalMinutes` | int | `10` | Allowlist re-fetch cadence |
| `maxListAgeHours` | int | `24` | Cache age beyond which new logins are refused |
| `failClosed` | bool | `false` | Refuse logins when the list cannot be fetched |
| `keypair` | generated | — | Config page can generate a fresh list keypair |

The config page shows a live preview: decrypted authorized npubs, last fetch time, event
age, and a warning when the list is plaintext.

### Login endpoint

`POST /NostrAuth/Login` — anonymous (no `[Authorize]`), reachable from the login screen.

- Request: `Authorization: Nostr <base64 event>` header per NIP-98; optional JSON body
  `{ deviceId, clientName, clientVersion }` for session metadata (informational only,
  never used for authorization).
- Response `200`: `AuthenticationResult`-shaped JSON (`user`, `sessionInfo`,
  `accessToken`, `serverId`) identical in shape to `POST /Users/AuthenticateByName`.
- Response `401`: `{ reason }` with machine-readable values:
  `invalid_event`, `expired_event`, `url_mismatch`, `method_mismatch`,
  `not_in_allowlist`, `allowlist_stale`, `allowlist_unavailable`.
- Response `503` only when `failClosed=true` and the list cannot be fetched.

### Login page snippet

A standalone JS snippet (Branding-injected) that presents: a "Sign in with Nostr" button
(NIP-07 if `window.nostr` exists), a bunker-paste field (`bunker://`), and a
`nostrconnect://` QR. All paths produce the NIP-98 header and POST to the login endpoint;
on success the snippet stores the access token the way the standard client does.

## Diagrams

    ┌─ one-time owner setup ─────────────────────────────────────────┐
    │ generate list keypair (config page) → import list nsec into    │
    │ any Nostr client → publish private kind-10000 list whose       │
    │ p-tags = authorized npubs → paste npub/nsec/relays into config │
    └────────────────────────────────────────────────────────────────┘

    allowlist sync (background)

      REQ kind 10000/30078 authors=[list npub]   (all configured relays)
        │
        v
      verify Schnorr sig against list npub ──invalid──> ignore event
        │ valid
        v
      newest created_at wins ──────────┐
        │                              │
        v                              v
      NIP-44-decrypt .content      plaintext p-tag fallback
        │                              │
        └──────────┬───────────────────┘
                   v
      allowlist cache (npubs, event age) ── poll loop ─┐
                                                       └── repeat

    login

      user picks signer (NIP-07 / bunker / Amber)
        │ signs kind 27235 {u: this URL, method: POST}
        v
      Authorization: Nostr <base64> ──> POST /NostrAuth/Login
        │
        v
      verify NIP-98 (sig, 60s, u, method) ──fail──> 401 reason
        │ valid
        v
      npub in allowlist cache? ──no──> 401 not_in_allowlist
        │ yes
        v
      npub → Jellyfin user (create if first login, unusable password)
        │
        v
      ISessionManager.AuthenticateDirect(...) ──> 200 AuthenticationResult

## See also

- `../tests/README.md` — how to run the behavior tests.
- `tests/Jellyfin.Plugin.NostrAuth.Tests/Nip98VerificationTests.cs` — NIP-98 acceptance
  and rejection matrix (signature, freshness, url/method binding, kind).
- `tests/Jellyfin.Plugin.NostrAuth.Tests/AllowlistSyncTests.cs` — list decryption,
  plaintext fallback, newest-wins selection, signature rejection, staleness and
  fail-open/closed behavior.
- `tests/Jellyfin.Plugin.NostrAuth.Tests/UserMappingTests.cs` — npub→user provisioning,
  reuse of existing users, unusable passwords, provider id assignment.
- `tests/Jellyfin.Plugin.NostrAuth.Tests/LoginEndpointTests.cs` — endpoint response
  contract (200 shape, 401 reason codes).
- `tests/fixtures/` — real signed NIP-98 and encrypted-list vectors generated by
  `tests/fixtures/generate.mjs` (nostr-tools); these fixtures are the contract inputs the
  C# implementation must accept and reject.
