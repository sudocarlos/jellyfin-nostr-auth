# Signing In

Open the server's Nostr login page (linked from the Jellyfin login screen via
Branding — see [Getting Started](getting-started.md#6-add-the-sign-in-link-for-viewers)).
All sign-in methods produce the same artifact — a NIP-98 signed event — so the
server verifies them identically.

## Browser extension (NIP-07)

If you use a NIP-07 extension (Alby, nos2x, …), press **Sign in with browser
extension**. The extension signs the NIP-98 event; nothing else is needed.

## Remote signer / bunker (NIP-46)

Paste a `bunker://` link from Amber, nsec.app, nsecBunker, or similar into the
**bunker** field and press **Sign in with bunker**. A NIP-05 identifier that
resolves to a bunker works too. The bunker signs the event remotely; your key
never leaves the bunker, and once the session is issued the bunker does not
need to be online again. Bunker responses are given a 60-second budget; after
that you get a retryable error.

## Sign with a QR (nostrconnect)

Press **Get a nostrconnect link** to receive a `nostrconnect://` QR. Open it
with your mobile signer (e.g. Amber), which connects back over the configured
relays. The page waits up to 60 seconds for the connection.

## What the page does

On success the page hands the issued session to jellyfin-web itself: it reads
the server id from `/System/Info/Public` and writes the access token into
jellyfin-web's own credential store (`jellyfin_credentials`), then redirects
to the web root — so the web client auto-signs in on the next load, exactly as
if you had logged in with a password.

## What you will see on failure

Errors are shown with a machine-readable reason from the server:

| Reason | Meaning |
|---|---|
| `invalid_event` | The signed event could not be read, verified, or bound |
| `expired_event` | The event is older/newer than the 60-second freshness window — check device clock |
| `url_mismatch` | The event was bound to a different URL; reload and retry |
| `method_mismatch` | The event was bound to a different HTTP method |
| `not_in_allowlist` | Your key is not on the server's allowlist |
| `allowlist_stale` | The server's cached list is past its max age; wait for the next sync |
| `allowlist_unavailable` | The relays are unreachable; the server refuses logins only if *fail closed* is on |
| `auth_rejected` | Server policy refused the session (device restrictions, session limit) |

## Notes

- Your username in Jellyfin is your bech32 npub. It is created on first
  successful login; an owner can rename it afterwards.
- If your key is removed from the allowlist, new logins are refused, but live
  sessions are not revoked — the owner can kill sessions from the dashboard.
- Native Jellyfin clients (TV/mobile apps) continue to require
  username/password and are out of scope.
