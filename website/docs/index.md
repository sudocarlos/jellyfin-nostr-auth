# Nostr Auth

A Jellyfin server plugin (Jellyfin 12.x, .NET 10) that replaces username/password
login with Nostr authentication for the web client. Viewers prove control of a
Nostr key with a [NIP-98](https://github.com/nostr-protocol/nips/blob/master/98.md)
signed event; access is authorized by the server owner's **private Nostr allowlist**.

[![GitHub Release](https://img.shields.io/github/v/release/sudocarlos/jellyfin-nostr-auth)](https://github.com/sudocarlos/jellyfin-nostr-auth/releases)
[![CI](https://github.com/sudocarlos/jellyfin-nostr-auth/actions/workflows/ci.yml/badge.svg)](https://github.com/sudocarlos/jellyfin-nostr-auth/actions/workflows/ci.yml)
[![Tests](https://img.shields.io/badge/tests-39%20passing-brightgreen)](https://github.com/sudocarlos/jellyfin-nostr-auth/actions/workflows/ci.yml)

📖 **[Full documentation](https://sudocarlos.github.io/jellyfin-nostr-auth/)**

## How it works

- **Sign in with any signer** - NIP-07 browser extensions (Alby, nos2x), NIP-46
  bunkers (Amber, nsec.app, nsecBunker), or a `nostrconnect://` QR. All produce
  the same NIP-98 event, so the server has one verification path.
- **Private allowlist authorization** - access is granted by a private NIP-51
  list (or a NIP-78 app-data list) authored by a dedicated list keypair,
  NIP-44-encrypted to itself, signature-verified, and synced over relays.
- **Automatic user provisioning** - first login creates a Jellyfin user named
  after the npub with an unusable random password, so password login stays
  closed for these accounts.
- **No identity keys held** - the plugin stores only the dedicated list nsec,
  which decrypts the allowlist and is trivially rotated.

## Install

Add the plugin repository under Dashboard → Plugins → Repositories:

```
https://sudocarlos.github.io/jellyfin-nostr-auth/manifest.json
```

Continue to [Getting Started](getting-started.md) to configure the plugin, or
straight to [Signing In](signing-in.md) if you are a viewer.

---

- [Issues & Pull Requests](https://github.com/sudocarlos/jellyfin-nostr-auth) - contributions welcome
- [Release notes](https://github.com/sudocarlos/jellyfin-nostr-auth/releases) - release history
- [Design document](https://github.com/sudocarlos/jellyfin-nostr-auth/blob/master/docs/design.md) - the full spec and rationale
