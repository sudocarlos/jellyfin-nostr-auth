# Jellyfin Nostr Auth

A Jellyfin server plugin (Jellyfin 12.x, .NET 10) that replaces username/password
login with Nostr authentication for the web client. Viewers prove control of a
Nostr key by presenting a NIP-98 signed event; access is authorized by the
server owner's **private Nostr allowlist**. See `docs/design.md` for the full
design and rationale.

## Layout

- `src/NostrAuth.Core` — pure logic, no Jellyfin dependencies:
  NIP-98 verification, allowlist sync (NIP-51 private lists / NIP-78 app data,
  NIP-44 decrypt-to-self), staleness policy, list keypair helpers.
- `src/Jellyfin.Plugin.NostrAuth` — the plugin: anonymous `POST /NostrAuth/Login`
  (NIP-98 → allowlist → provision → session), admin endpoints for the dashboard,
  relay sync host, npub→user provisioning.
- `tests/Jellyfin.Plugin.NostrAuth.Tests` — behavior tests against committed,
  real signed fixtures.
- `tests/fixtures/generate.mjs` — regenerates the signed fixtures with
  nostr-tools (fixed throwaway keys and timestamps, byte-for-byte reproducible).

## Development

Builds and tests run through the devcontainer image (`mcr.microsoft.com/dotnet/sdk:10.0`),
which is what CI uses too. There is no .NET SDK on the host.

```
podman run --rm -v "$PWD":/src -w /src mcr.microsoft.com/dotnet/sdk:10.0 \
    dotnet test Jellyfin.Plugin.NostrAuth.sln
```

Behavior expectations live in `tests/README.md`; the design contract in
`docs/design.md`. Every commit must build and pass tests.

## Packaging

`jprm` builds the installable zip from `build.yaml`:

```
jprm plugin build .
```

`build.yaml` pins `targetAbi: 12.1.0.0` (matching the packages we compile
against) and lists the plugin, core, and third-party runtime assemblies
explicitly — jprm ships only the listed artifacts. Host-provided
`Jellyfin.*`/`MediaBrowser.*` assemblies are never shipped
(`ExcludeAssets=runtime` in the plugin csproj). On GitHub, the packaging
workflow (`.github/workflows/build.yaml`) calls Jellyfin's shared
meta-plugins build for every push.

## Login flow

1. Owner generates a dedicated list keypair on the plugin's dashboard page.
2. Owner imports the list nsec into any Nostr client and publishes a private
   kind-10000 list whose p-tags are the authorized npubs.
3. Owner configures list npub/nsec/relays in the dashboard; the plugin syncs
   the list over the configured relays on a poll interval.
4. Viewers log in on the Nostr login page with a NIP-07 extension, a NIP-46
   bunker, or Amber; the plugin verifies the signed event, checks the
   allowlist, provisions the user on first login, and issues a Jellyfin
   session.
