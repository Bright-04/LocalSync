# 0001 — Restructure from prototype

- **Status:** Accepted
- **Date:** 2026-09-08

## Context

The tree before this change presented itself as a "production-grade LAN file
transfer platform" but did not build, and its documentation described software
that did not exist. Specifically:

- `backend/LocalSync.Worker/` was referenced from four places
  (`LocalSync.slnx`, `LocalSync.Api.csproj`, `LocalSync.Tests.csproj`, and a DI
  registration in `Program.cs`) and did not exist on disk. The solution could
  not restore.
- The web UI's `tsconfig.json` referenced `tsconfig.app.json` and
  `tsconfig.node.json`, neither of which existed, and there was no
  `index.html`. `npm run dev`, `build` and `lint` all failed.
- `docker-compose.yml` built three Dockerfiles, none of which existed.
- The README claimed .NET 8 (every project targeted `net10.0`), a
  `FileSystemWatcher`-based background worker, resumable transfers, and CI via
  GitHub Actions. There was no `.github/` directory.
- Commits `fde4222..c3e3b56` — the ten most recent — modified only
  `patch.tmp`, a one-line scratch file, while carrying messages such as
  "fix: resolve race condition in file queue" and "fix: handle large file
  uploads (>2GB)" that bear no relation to their diffs.

The layering underneath was nonetheless sound: `Core → Infrastructure → Api`
with dependencies pointing inward, and chunked transfer with browser-side
slicing and server-side positional writes is the right shape.

Two security defects also required naming, because they are live in the tagged
prototype:

- Unsanitized `request.FileName` reached `Path.Combine` under a fixed root, and
  `Path.Combine` with an absolute second argument discards the root entirely —
  an arbitrary file write.
- The chunk endpoint seeked to a completely unvalidated client-supplied
  `offset`. A single unauthenticated request with `offset = 2^40` creates a
  1 TiB sparse file.

## Decision

Treat the previous tree as a prototype rather than a baseline.

1. **Do not rewrite history.** The commits are already on the remote, and
   force-pushing to correct commit messages buys nothing. The tip before this
   change is tagged `prototype-v0` so it stays findable.
2. **Record the discrepancy here** rather than in commit messages, so a future
   reader who consults `git log` for intent is not misled by it.
3. **Restructure** to `src/` `tests/` `web/` via `git mv`, preserving history.
4. **Delete rather than create `LocalSync.Worker`.** Continuous folder sync is
   not a v1 capability; when it returns it returns as a `BackgroundService`
   inside the daemon, because a separate process would mean cross-process
   SQLite WAL contention on a chunk-granularity write path, two copies of the
   trust store, and the device private key loaded twice.
5. **Delete `docker-compose.yml`.** Packaging is native single-file binaries
   plus OS packages, not containers.
6. **Make documentation drift a build failure**, not a matter of discipline —
   the root cause of the state above was an absent feedback loop, not
   carelessness. `docs-lint` in CI greps for the specific false claims, checks
   that every referenced workflow path exists, and asserts the README's stated
   toolchain versions match `global.json` and `.nvmrc`.
7. **Adopt Conventional Commits**, squash-merge only, with branch protection on
   `master`, so the commit/diff mismatch cannot recur.

## Consequences

- The solution builds and the frontend's three scripts work.
- `git log` for `patch.tmp` remains misleading; this record is the mitigation.
- Anyone bisecting past `prototype-v0` will find a tree that does not restore.
  That is a property of the history, not of this change.
- The two security defects are fixed structurally rather than by sanitisation
  alone: received bytes are written to server-generated ordinal filenames in a
  per-transfer staging directory, so a client-supplied name never touches a
  write path, and offsets are bounds-checked against the declared session size.
  See ADRs for the transfer engine when they land.
