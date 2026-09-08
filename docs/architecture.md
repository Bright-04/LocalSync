# Architecture

## Project graph

```
Core          →  (nothing but the BCL)
Infrastructure→  Core
Api           →  Core, Infrastructure
```

Target state, as projects land:

```
Core          →  (nothing but the BCL)
Protocol      →  Core
Infrastructure→  Core, Protocol
Host          →  Core, Protocol, Infrastructure  + FrameworkReference AspNetCore
WebUi         →  (nothing)
Cli           →  Host, WebUi
Mobile        →  Core, Protocol, Infrastructure
```

## Dependency rules

These are rules, not preferences. Each one is (or will be) enforced by a
`NetArchTest` assertion in `tests/LocalSync.Core.Tests`, because a rule written
only in a markdown file demonstrably does not hold.

1. **`Core` references nothing outside the base class library.** No
   `PackageReference`, no `ProjectReference`, and never `Microsoft.AspNetCore.*`.
   This is what keeps the assembly consumable by the MAUI head.
2. **`Infrastructure` must not reference `Microsoft.AspNetCore.*`.** Anything
   needing `HttpContext` belongs in `Host`.
3. **`Mobile` must never reference `Host`, `WebUi`, or `Cli`.**
4. **`WebUi` is referenced by `Cli` alone.** If `Host` referenced it, every
   `dotnet test` would trigger an `npm install`.
5. **No `DateTime.UtcNow` in `Core`.** Inject `TimeProvider`; every TTL,
   timeout, retry backoff and expiry in this system is time-driven and must be
   testable with `FakeTimeProvider`.

## NativeAOT

The desktop daemon ships as a trimmed, single-file NativeAOT binary. That
constraint propagates further than it first appears:

- **MVC is unsupported under NativeAOT.** `[ApiController]`, `[FromForm]` and
  `IFormFile` all bind by reflection, so the HTTP surface must be Minimal APIs.
- **SignalR is only partially supported**, and strongly-typed hubs not at all.
  The realtime channel is Server-Sent Events instead, which also removes a
  client-side dependency from the zero-install browser bundle.
- **`Serilog.Settings.Configuration` is not trim-compatible.** Logging is
  configured in code, never from `appsettings`.

`src/Directory.Build.props` sets `IsAotCompatible=true` on every project under
`src/`, which enables the trim, AOT and single-file analyzers. Combined with
`TreatWarningsAsErrors`, IL2xxx/IL3xxx diagnostics surface at `dotnet build` on
a developer machine rather than at `dotnet publish` in a release job weeks
later. Opt-outs are permitted but must carry a comment saying why and when they
go away — see `src/LocalSync.Api/LocalSync.Api.csproj`.

## Listeners

The daemon exposes four distinct surfaces. Two TLS configurations are required
rather than merely convenient: compat peers cannot present a client
certificate, so `ClientCertificateRequired` must differ per listener.

| Port | TLS | Client cert | Serves |
|---|---|---|---|
| `53318` | 1.3 | Required | `/api/localsync/v1/*` — native peers |
| `53317` | 1.2+ | Not requested | `/api/localsend/v2/*` — LocalSend compat |
| `53319` | 1.2+ self-signed | Not requested | `/b/*` — guest browser |
| UDS / named pipe | — | OS file mode | CLI control plane |

Native and compat requests are distinguished by *listener plus validated client
certificate*, never by a request header, which would be spoofable.

## Trust model

Device identity is an ECDSA P-256 key pair generated on first run.
`DeviceId = SHA-256(SubjectPublicKeyInfo)`.

The **SPKI** is pinned, not the certificate. Pinning a certificate hash — as
LocalSend does — means certificate renewal changes the device's identity and
forces every peer to re-pair. Pinning the public key lets certificates rotate
freely while pairings survive indefinitely.

Discovery is unauthenticated by construction and always will be, so nothing
arriving over multicast may be an input to an authorization decision. An
advertised device ID is used only to look up an already-pinned key and to pick
a candidate address. A known ID appearing at a new address is normal (DHCP); a
known ID presenting a *different key* is an attack or a reinstall and requires
explicit re-pairing, never a silent trust-on-first-use update.

## Further reading

- [`adr/`](adr/) — numbered decision records
- [`protocol/`](protocol/) — the native wire protocol and LocalSend compat notes
