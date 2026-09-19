# Development

The repo builds and tests through the devcontainer image
(`mcr.microsoft.com/dotnet/sdk:10.0`) — the same image CI uses. There is no
.NET SDK on the host.

## Build and test

```
podman run --rm -v "$PWD":/src -w /src mcr.microsoft.com/dotnet/sdk:10.0 \
    dotnet test Jellyfin.Plugin.NostrAuth.sln
```

Behavior expectations live in `tests/README.md`; the design contract in
`docs/design.md`. Every commit must build and pass tests.

## Layout

- `src/NostrAuth.Core` — pure logic, no Jellyfin dependencies: NIP-98
  verification, allowlist sync (NIP-51 private lists / NIP-78 app data,
  NIP-44 decrypt-to-self), staleness policy, keypair helpers.
- `src/Jellyfin.Plugin.NostrAuth` — the plugin: anonymous `POST /NostrAuth/Login`,
  admin endpoints, relay sync host, npub→user provisioning, dashboard config
  page, pre-login Nostr page.
- `tests/Jellyfin.Plugin.NostrAuth.Tests` — behavior tests against committed,
  real signed fixtures.

## Fixtures

`tests/fixtures/generate.mjs` produces deterministic Nostr fixtures with
nostr-tools (fixed throwaway keys, fixed timestamps, fixed signing nonce, and
a deterministic NIP-44 nonce). Outputs must be byte-for-byte reproducible —
CI regenerates and fails on drift:

```
cd tests/fixtures && npm ci && node generate.mjs
```

Commit the outputs so C# tests never depend on network or signing at test
time.

## Login page bundle

`src/Jellyfin.Plugin.NostrAuth/Web/nostr.mjs` is a vendored, minified bundle
of nostr-tools — the same version `tests/fixtures` signs fixtures with, so
client↔server interop is pinned. Regenerate after bumping nostr-tools:

```
cd tests/fixtures
npx esbuild snippet-entry.mjs --bundle --format=esm --platform=browser \
    --target=es2020 --minify \
    --outfile=../../src/Jellyfin.Plugin.NostrAuth/Web/nostr.mjs
```

## Packaging

`jprm` builds the installable zip from `build.yaml`:

```
jprm plugin build .
```

`build.yaml` pins `targetAbi: 12.1.0.0` (matching the packages we compile
against) and lists the plugin, core, and third-party runtime assemblies
explicitly — jprm ships only the listed artifacts. Host-provided
`Jellyfin.*`/`MediaBrowser.*` assemblies are never shipped
(`ExcludeAssets=runtime` in the plugin csproj).

## CI and release

- `ci.yml` — fixture determinism + behavior tests on every push.
- `.github/workflows/build.yaml` — Jellyfin's shared meta-plugins build for
  packaging on every push.
- `publish.yaml` — on release: attaches the zip + checksums to the release
  and updates the plugin catalog (`manifest.json`) on the gh-pages branch.

## This website

The docs are [MkDocs Material](https://squidfunk.github.io/mkdocs-material/)
under `website/`. Build locally:

```
cd website && pip install -r requirements.txt && mkdocs serve
```

The site deploys to the same `gh-pages` branch that hosts the plugin catalog
(`manifest.json`), so the catalog URL and the docs share one Pages site.
