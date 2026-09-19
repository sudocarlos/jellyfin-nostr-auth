# Troubleshooting

## Logins fail with `allowlist_unavailable`

The relays cannot be reached or return no valid list event. Check:

- The **Relays** field uses `wss://` URLs and the events exist on them.
- The **List event kind** matches what you published (`10000` vs `30078`).
- The list npub in the configuration is the one that signed the event.

By default the last known list stays honored during outages; logins only fail
on this reason when **Fail closed** is enabled — otherwise you see
`not_in_allowlist` or stale behavior instead.

## Logins fail with `not_in_allowlist` for an authorized key

- Confirm the npub is in the list **event** (the preview panel shows exactly
  what the server decrypted, as bech32 npubs).
- If you republished the list recently, wait for the next poll or increase
  freshness; the cache holds the newest signature-valid event.
- If the preview shows no snapshot at all, no valid list event was fetched
  yet — new logins are refused until the first sync succeeds.

## Logins fail with `expired_event`

The NIP-98 freshness window is 60 seconds in both directions. A skewed clock
on the viewing device is the usual cause; also verify the signer produced a
fresh event (some stale bunker sessions replay old events).

## The allowlist preview says the list is plaintext

Some clients publish private lists with plaintext p-tags. Such a list is
effectively public: anyone reading the relay can see the authorized npubs.
Republish as a NIP-51 private list (kind `10000`) — the plugin reads the
encrypted form when present.

## Viewer cannot reach the login page

The login page lives at `<server>/NostrAuth/LoginPage`. If you configured a
Jellyfin **base URL**, the page moves under it as well — link via the
Branding field with a path relative to the web root, e.g.
`/jellyfin/NostrAuth/LoginPage`. The link belongs in Dashboard → General →
Branding (login disclaimer or custom CSS).

## Behind a reverse proxy

The NIP-98 `u` tag binds the event to the absolute request URL, scheme
included. If the proxy terminates TLS, make sure Jellyfin's **Known proxies**
is configured so the server reconstructs `https://…` URLs — otherwise
signers bind to `https` while the server computes `http` and every login
fails with `url_mismatch`. This is the same forwarded-headers setup Jellyfin
already requires for other absolute-URL features.

## A provisioned user shows up but cannot log in with a password

Intentional. Provisioned users carry an unusable random password and an
`AuthenticationProviderId` that names no password provider — only the Nostr
login flow can mint their sessions. Owners can manage their permissions from
the dashboard like any other user.

## The plugin is not listed in the catalog

The catalog manifest targets `targetAbi 12.1.0.0`; it is not offered to
servers older than that. Check Dashboard → Diagnostics → About for your
server version, and refresh the plugin catalog.
