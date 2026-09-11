# LocalSync

LAN-first peer-to-peer file transfer. Devices find each other on the local
network and send files directly, with no cloud, no account, and no relay.

> **Status: early development.** The repository is being restructured from a
> prototype into a working product. The table below is the honest state of play —
> nothing is listed as shipped until a test asserts it.

## Feature status

| Capability | Status |
|---|---|
| Peer discovery on the LAN | 🚧 In progress — being rewritten as a UDP multicast responder |
| One-shot file send / receive | 🚧 In progress |
| Web dashboard | 🚧 In progress |
| Resumable transfers | 📋 Planned |
| Pinned public-key device pairing (QR + short code) | 📋 Planned |
| Headless daemon + CLI + REST API | 📋 Planned |
| Parallel multi-stream transfer | 📋 Planned |
| Zero-install browser receive | 📋 Planned |
| LocalSend v2.2 interoperability | 📋 Planned |
| Mobile (Android / iOS) | 📋 Planned |
| Continuous folder sync | 📋 Planned — deferred past v1 |

Nothing above is shipped yet. Do not use this for data you care about.

## Design goals

- **Lightweight** — a single self-contained binary per platform, no runtime install.
- **MITM-proof** — device identity is a pinned public key, confirmed out-of-band
  by QR scan or a short code. Discovery announcements are treated as routing
  hints, never as trust inputs.
- **Broadly cross-platform** — Windows, macOS, Linux, Android, iOS.
- **Interoperable** — speaks the LocalSend v2.2 protocol alongside its own.

See [`docs/architecture.md`](docs/architecture.md) for the project layout and
dependency rules, and [`docs/adr/`](docs/adr/) for the reasoning behind the
significant decisions.

## Prerequisites

| Tool | Version | Notes |
|---|---|---|
| .NET SDK | 10.0.100 or later in that feature band | Pinned by `global.json`. Install the official package from [dotnet.microsoft.com](https://dotnet.microsoft.com/download/dotnet/10.0) rather than a package manager. |
| Node.js | 24 | Pinned by `.nvmrc`. Only needed to work on the web UI. |

## Building

```bash
# Backend
dotnet restore
dotnet build
dotnet test

# Web UI
cd web/localsync-ui
npm ci
npm run build
```

## Developing the web UI

The dev server proxies API and event-stream traffic to a locally running
daemon, so hot reload works without rebuilding the backend:

```bash
cd web/localsync-ui
npm run dev      # http://localhost:5173
npm run lint     # Biome: format + lint in one pass
npm run format   # apply formatting
```

## Repository layout

```
src/LocalSync.Core/            domain models and interfaces, BCL only
src/LocalSync.Infrastructure/  discovery, transport, storage
src/LocalSync.Api/             HTTP host (being split into Host + Cli)
tests/                         test projects
web/localsync-ui/              React + Vite dashboard
docs/                          architecture, ADRs, protocol specs
```

`LocalSync.Core` deliberately references nothing outside the base class
library, so the same assembly can be consumed by the mobile head. That rule is
enforced by a test, not by convention.

## Continuous integration

[`.github/workflows/pr.yml`](.github/workflows/pr.yml) runs on every pull
request: restore, format check, build, test, an AOT analyzer pass over the
libraries, the web lint/typecheck/build, and a documentation lint that fails if
this file drifts from reality.

## Licence

Not yet chosen.
