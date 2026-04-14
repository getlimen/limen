# Plan 07 — Polish: full E2E suite, READMEs, release pipeline

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` or `superpowers:executing-plans`.

**Goal:** Raise quality to release-readiness. Full integration test suite runs all 4 components together. Per-repo READMEs with install instructions. Docs site scaffolded. Release pipeline tags semver versions and publishes images.

**Prerequisites:** Plans 1-6 complete.

---

## Tasks

### Task 1: E2E test harness

Create `limen/tests/e2e/` with:
- `compose.e2e.yml` — all 4 services + Postgres + Pebble (LE test server) + a fake Docker registry + 2 "fake remote nodes" (two more Limentinus containers with `docker` role)
- `E2ETests.csproj` — xUnit + Playwright
- Global fixture spins up compose, runs migrations, seeds admin OIDC (via static test IdP)

Scenarios covered:
1. **Enrollment end-to-end** — generate key, run second Limentinus, node becomes Active with WG tunnel
2. **Deploy + auto-update** — service points at fake registry image, digest change triggers redeploy
3. **Rollback** — bad image deploys, health-check fails, old container restored
4. **Route exposure with TLS** — create route, Pebble issues cert, curl through YARP
5. **Auth flows** — password, magic-link, OIDC
6. **Resilience** — kill Limen, restart, ensure everything reconverges
7. **Revocation** — revoke token, within 30s further requests blocked

### Task 2: Per-repo README.md

**`limen/README.md`:**
- What Limen is (2 sentences)
- Quick start: `git clone`, copy `template.env` → `.env`, `docker compose up -d`
- Point your OIDC at it, sign in
- Add a node via provisioning key
- Add a service, add a route, done
- Links to docs site

**`ostiarius/README.md`:**
- Role (public-facing reverse proxy with auto-TLS)
- Not installed directly; brought up by Limentinus on proxy-role nodes
- Link to design spec

**`forculus/README.md`:**
- Role (WireGuard hub)
- Runs alongside Limen on control-role node
- Config sync via HTTP from Limen
- Link to design spec

**`limentinus/README.md`:**
- Role (universal node agent)
- Install: `docker compose up` with env vars (`LIMEN_CENTRAL_URL`, `LIMEN_PROVISIONING_KEY`, `LIMEN_ROLES`)
- Platforms: linux/amd64, linux/arm64

**`limen-cli/README.md` (stub):** "Planned for v1.1"

**`limen-docs/README.md` (stub):** "Planned; Astro-based, deployed to GitHub Pages"

**`.github/profile/README.md`:** org landing page with logo, one-liner, links to each repo

### Task 3: Organization `.github` profile

Create `.github/profile/README.md` with:
- Logo SVG (from the art plan)
- One-paragraph description of Limen
- Grid of the 4 core components with brief descriptions
- Latin etymology blurb
- License: Apache 2.0

### Task 4: Docs site scaffold (`limen-docs`)

Minimal Astro Starlight site with:
- Home page (repurposed README content)
- Quickstart guide
- Architecture overview (repurposed spec)
- Links to each component's spec section
- Deploy to GitHub Pages via workflow

Defer content authoring beyond skeleton until after v1 ships.

### Task 5: Release pipeline

For each repo: `.github/workflows/release.yml` — on semver tag push (`v*.*.*`):
1. Build + test
2. Build multi-arch Docker image (linux/amd64, linux/arm64)
3. Push to `ghcr.io/getlimen/<repo>:v<tag>` + `:latest` + `:sha-<short>`
4. Create GitHub Release with changelog (from conventional commits)
5. For `limen`: publish `Limen.Contracts` NuGet to GitHub Packages

### Task 6: Limen.Contracts NuGet publish

Add `pack-nuget.yml` workflow to `limen`:
- On push to main in `contracts/`: `dotnet pack`, `dotnet nuget push` to `https://nuget.pkg.github.com/getlimen/index.json`
- Other repos: `<PackageSource><add key="getlimen" value="..." /></PackageSource>` in NuGet.config
- Add NuGet.config to each repo referencing the feed

### Task 7: Backup + restore documentation

`limen/docs/operations/backup-restore.md`:
- What to back up: `limen` Postgres, `forculus` `/etc/wireguard/*.conf` (though re-derivable from Limen), `ostiarius` `/data/certs` (re-derivable via ACME)
- Backup commands (`pg_dump`)
- Restore procedure: restore DB, redeploy compose, agents auto-reconnect

### Task 8: Monitoring & observability

Add OpenTelemetry traces to all four components (OTLP endpoint configurable via env). Optional Prometheus metrics endpoints. Skip actual dashboard integration (document for v2).

### Task 9: Security hardening

- All Docker images run as non-root user (except Forculus which needs NET_ADMIN)
- Kestrel binds to specific interfaces only (public on Ostiarius; internal on others)
- Rate limiting on login endpoints
- CSRF tokens on admin UI state-changing endpoints
- Content Security Policy header on Angular app

### Task 10: Release v0.1.0

1. Tag `v0.1.0` in each repo
2. Wait for release workflow
3. Write a launch announcement post (for repo root/README + `limen-docs/blog/`)
4. Submit to awesome-selfhosted list

---

## Exit criteria for Plan 7

✅ Full E2E suite runs green in CI nightly
✅ Per-repo READMEs for all 7 repos
✅ Docs site at limen-docs with quickstart + architecture pages
✅ Release pipeline: `v*.*.*` tag → multi-arch images in ghcr
✅ Limen.Contracts published to GitHub Packages
✅ Backup/restore documented
✅ OpenTelemetry wiring in place
✅ v0.1.0 released

**Limen v1 is shipped.**

Post-v1 work continues per `docs/roadmap.md`.
