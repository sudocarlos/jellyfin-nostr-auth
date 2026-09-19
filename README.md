# jellyfin-nostr-auth

A Jellyfin server plugin (Jellyfin 12.x) that replaces username/password login
with Nostr authentication for the web client. Viewers prove control of a Nostr
key with a NIP-98 signed event; access is authorized by the server owner's
**private Nostr allowlist**.

[![GitHub Release](https://img.shields.io/github/v/release/sudocarlos/jellyfin-nostr-auth)](https://github.com/sudocarlos/jellyfin-nostr-auth/releases)
[![CI](https://github.com/sudocarlos/jellyfin-nostr-auth/actions/workflows/ci.yml/badge.svg)](https://github.com/sudocarlos/jellyfin-nostr-auth/actions/workflows/ci.yml)
[![Build Plugin](https://github.com/sudocarlos/jellyfin-nostr-auth/actions/workflows/build.yaml/badge.svg)](https://github.com/sudocarlos/jellyfin-nostr-auth/actions/workflows/build.yaml)

📖 **[Full documentation](https://sudocarlos.github.io/jellyfin-nostr-auth/)**

## Quick Start

Add the plugin repository under Dashboard → Plugins → Repositories:

```
https://sudocarlos.github.io/jellyfin-nostr-auth/manifest.json
```

Then, in the plugin's dashboard page: generate a list keypair, publish a
private NIP-51 list with the authorized npubs from any Nostr client, and point
the plugin at it. See [Getting Started](https://sudocarlos.github.io/jellyfin-nostr-auth/getting-started/).

---

- [Issues & Pull Requests](https://github.com/sudocarlos/jellyfin-nostr-auth) — contributions welcome
- [Releases](https://github.com/sudocarlos/jellyfin-nostr-auth/releases) — installable plugin zips and release history
- [Design document](https://github.com/sudocarlos/jellyfin-nostr-auth/blob/master/docs/design.md) — the full spec and rationale
