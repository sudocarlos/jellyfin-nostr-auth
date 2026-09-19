# Getting Started

Setup is one-time for the server owner: generate a dedicated list keypair,
publish a private list from any Nostr client, and point the plugin at it.

## 1. Install the plugin

Add the repository under Dashboard → Plugins → Repositories:

```
https://sudocarlos.github.io/jellyfin-nostr-auth/manifest.json
```

Install **Nostr Auth** from the catalog and restart Jellyfin when prompted.
The plugin targets Jellyfin 12.x (`targetAbi 12.1.0.0`).

## 2. Open the plugin configuration

Dashboard → Plugins → **Nostr Auth**. The page has two parts: the settings
form and a live **Allowlist preview**.

## 3. Generate the list keypair

Click **Generate fresh list keypair**. This fills the *List npub* and *List
nsec* fields. The keypair is dedicated to the allowlist: whoever holds the
nsec can read and rewrite this one server's access list, and nothing else.
It is trivially rotated — generate a new pair, republish, save.

Nothing is persisted until you press **Save**.

## 4. Publish the private allowlist

Import the list nsec into any Nostr client and publish a list whose p-tags
are the npubs you want to authorize:

=== "NIP-51 private list (recommended)"

    Author a **private list** (kind `10000`) — authorable in every mainstream
    client. Private items are stored in the event content as a NIP-44-encrypted
    tags-array, so the list stays private on the relay.

    The plugin decrypts it with the list nsec from its configuration.

=== "NIP-78 app data (advanced)"

    Publish a kind `30078` event with `d="jellyfin-allowlist"` whose decrypted
    content is `{"allowed": ["<hex pubkey>", ...]}`. Cleaner semantics, but no
    mainstream client authors it for you.

Set **List event kind** accordingly. Whichever kind you choose, some clients
publish private lists in plaintext p-tags — the plugin reads that as a
fallback but shows a **plaintext warning** in the dashboard, since such a list
is effectively public.

## 5. Configure the plugin

| Setting | Meaning |
|---|---|
| List npub / List nsec | The dedicated keypair from step 3 |
| Relays | Comma-separated `wss://` relays used to fetch the list |
| List event kind | `10000` or `30078`, matching what you published |
| Poll interval | Allowlist re-fetch cadence (default 10 min) |
| Max list age | Cache age beyond which new logins fail closed (default 24 h) |
| Fail closed | Refuse logins while the list cannot be fetched (default off) |

Save. The sync host fetches the list from every configured relay, keeps the
newest signature-valid event, and caches the decrypted npubs. The preview on
the configuration page shows the authorized users, last fetch time, and
staleness state.

## 6. Add the sign-in link for viewers

jellyfin-web has no plugin hook to replace the login page, so point viewers at
the plugin's own page via Dashboard → General → Branding — either the
**Login disclaimer** field or custom CSS, e.g.:

```html
<a href="/NostrAuth/LoginPage">Sign in with Nostr</a>
```

Viewers then follow [Signing In](signing-in.md).

## Trust boundaries

The plugin never holds your identity nsec. Compromise of the list nsec only
reveals and rewrites this server's access list — rotate by generating a new
keypair and republishing. NIP-05 identifiers, NIP-46 client metadata,
relay-supplied metadata, and your owner npub are explicitly **not** trusted
for authorization: only verified signatures over the allowlist and the
NIP-98 login event matter.
