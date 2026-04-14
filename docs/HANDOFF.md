# Limen — Handoff Document

**Purpose:** This document gives any new agent (or human) full context on the Limen project — what it is, what was decided during design, what's already written, and what's left to build. Read this first.

**Last updated:** 2026-04-14
**Current phase:** Design complete. Implementation plans written. No code scaffolded yet. GitHub org creation pending human action.

---

## 1. What is Limen?

**Limen** *(Latin: "threshold")* is a self-hosted infrastructure platform combining three capabilities normally requiring three separate tools:

1. **Docker deploy management** (like Coolify) — auto-deploy services from registry image updates
2. **Reverse proxy with TLS** (like Traefik/Caddy) — public ingress, routing, Let's Encrypt
3. **WireGuard hub-and-spoke VPN** (like Pangolin) — connect remote sites without public inbound ports

Single admin, self-hosted via Docker compose. Written in C# (.NET 10) + Angular 21. Postgres-only datastore.

**Full design:** see `docs/superpowers/specs/2026-04-14-limen-design.md` — authoritative.

---

## 2. Decision history — what was decided and why

This section captures the reasoning behind each locked decision, so you understand *why* things are the way they are (not just what they are). Full spec has the current state; this file has the "how we got there."

### 2.1 — Name: Limen

- **Latin for "threshold"** — the stone at the bottom of a Roman doorway
- Chosen after rejecting: `UDKRPAPM` (original user suggestion, unusable), `Janus` (taken by HelloFresh as an API gateway, by Meetecho as WebRTC, by ACM paper as container orchestrator), `Portus` (Cableguard WireGuard conflict), `Bastion` (direct competitor `Miles0sage/bastion`), `Pharos` (Kontena Kubernetes distro), `Terminus` (TerminusDB, godaddy/terminus both huge)
- **Limen has zero conflicts in our space**. Only prior-art uses are a Python DRM lib, a JS stock tool, and a VHDL processor — all irrelevant.
- Pronunciation: `/ˈliː.mən/` — "LEE-men"

### 2.2 — Org: `getlimen`

- `limen` and `limen-io` on GitHub are **both taken**
- `getlimen` follows established OSS naming convention (getsentry, getmeili, getlago, getoutline)
- **Org does not yet exist on GitHub.** User must create manually at https://github.com/organizations/new

### 2.3 — Component names (Roman doorway theme)

| Component | Latin meaning | Role |
|-----------|---------------|------|
| **Limen** | threshold stone | Central manager (C# + Angular + Postgres) |
| **Ostiarius** | doorkeeper/porter | Reverse proxy (C# NativeAOT + YARP + LettuceEncrypt-Archon) |
| **Forculus** | god of door panel | WireGuard hub (C# NativeAOT + `wg` CLI subprocess) |
| **Limentinus** | guardian of thresholds | Universal node agent (C# NativeAOT + wireguard-go userspace) |

- All four names are Romans' specific door deities/concepts (they had a named figure for each part of a door)
- Verified low/zero conflicts on GitHub before locking
- Ostiarius has one minor conflict: a 1⭐ dormant "Simple API Gateway" from a student org; accepted

### 2.4 — Scope choice: minimum slice, not full reimplementation

User considered three paths:
- (a) minimum differentiating slice first ← **chosen**
- (b) thin skeleton across all 5 subsystems
- (c) wrap Coolify + existing tools (rejected — Coolify is PHP/Laravel, wrap-layer in C# is awkward)

**V1 ships:** WireGuard + reverse proxy + Docker-deploy management + Angular UI.
**V2+ deferred:** K8s/k3s, multi-admin RBAC, config-as-code, CLI, HA control plane, enterprise edition.

### 2.5 — Tech stack

Mirrors Niclas's existing Kursa project (most recent C# reference):

- **Backend:** .NET 10 / ASP.NET Core
- **Architecture:** Clean / onion with strict layer rules (see §2.11)
- **Frontend:** Angular 21 + spartan.ng + Tailwind CSS 4
- **ORM:** EF Core (code-first, Postgres provider)
- **DB:** PostgreSQL — **sole datastore**. No Redis, no MinIO, no vector DB. User was explicit.
- **Background jobs:** **Quartz.NET** (Apache 2.0) — NOT Hangfire (LGPL + paid Pro)
- **Cache:** `IMemoryCache` (in-process) — Redis replacement
- **Pub/sub (if needed):** Postgres `LISTEN/NOTIFY`
- **Reverse proxy engine:** YARP (MIT, Microsoft) + LettuceEncrypt-Archon (active fork after original went into maintenance)
- **WG in Forculus:** subprocess `wg` CLI tool (same pattern as Gerbil, adapted for C#)
- **WG in Limentinus:** embedded `wireguard-go` userspace binary + UAPI socket (same as Pangolin's Newt)
- **Agent/proxy binaries:** C# NativeAOT (small single binaries, cross-platform, stack uniformity)

### 2.6 — No jobs dashboard

User asked why we need a dashboard for background jobs. Correct answer: we don't. Limen's main UI surfaces deploy/sync status as first-class features; a Hangfire-style internal jobs dashboard would be duplicate work. Quartz.NET has no dashboard and doesn't need one.

### 2.7 — Admin-only (single identity class for admin plane)

There is no multi-user admin system in v1. There's one admin per Limen install. **Resource-level authentication (§10 of spec) is a separate identity plane** — it authenticates *users hitting protected resources Limen exposes*, not admins of Limen itself.

### 2.8 — Deployment: Docker compose only

No single-binary install. No ISO image. No K8s. User was explicit: **always Docker-based, nothing else**.

### 2.9 — Node model: role flags, not node types

Every node runs Limentinus (the universal agent). Roles (`control`, `proxy`, `docker`) are flags determining which additional containers Limen brings up on that node. The control node can also have `docker` and/or `proxy` — no special node types.

### 2.10 — Custom reverse proxy (Ostiarius) instead of Caddy/Traefik

User chose to build a custom C# proxy on YARP rather than running Caddy or Traefik as a sidecar. Trade-offs:
- **Pro:** stack uniformity (all C#), tight integration with Limen's WS config push
- **Con:** more work than using Traefik; diverges from every reference project (Pangolin/Coolify/Portainer all use Traefik)
- **Honest flag:** this is the biggest architectural risk in v1 scope. If Ostiarius slips, falling back to Traefik as temporary data plane is an option.

### 2.11 — Clean architecture strict rules (from user — memory saved)

These apply to **every** C# project in the org:

- **Domain layer** — ONLY database entity models. No value objects, no enums, no services, no interfaces. Just classes that map to DB tables.
- **Infrastructure layer** — ALL DB code (EF, migrations, repositories) AND all external integrations (WS/HTTP clients, OIDC, Quartz jobs, Docker API, subprocesses, Kestrel/YARP wiring, ACME).
- **Application layer** — EVERYTHING else: services, commands, queries, DTOs, validators, interfaces, Mediator behaviors. All business logic.

**Sub-structure of Application:**
- Top-level `Commands/` and `Queries/` folders (capitalized)
- Organized by feature: `Commands/Nodes/`, `Queries/Services/`, etc.

**One file per command or query:** the file contains BOTH the type AND its handler. Example: `CreateServiceCommand.cs` contains `CreateServiceCommand` record + `CreateServiceCommandHandler` class in the same file. Do NOT split them into separate files.

**Memory file:** `~/.claude/projects/C--GithubProjects/memory/feedback_clean_architecture_layout.md`

### 2.12 — Commit convention

**NO AI/Claude attribution in commits or PRs.** No `Co-Authored-By: Claude`, no 🤖 trailers, no "Generated with Claude Code". This is inherited from the user's other repos (Kotlin-tls-client, KotifyClient, AutexisCase `.serena/memories/code_style_and_conventions.md`).

### 2.13 — Language

English only for all code, comments, commits, and documentation. User is Swiss (native German speaker) but all repos are English-first — matches existing pattern.

### 2.14 — License

**Apache 2.0** for all repos in v1. Permissive, enterprise-friendly patent grant. Easy to dual-license later if the project goes open-core for enterprise sales (v3+ roadmap).

### 2.15 — Authentication

- **Admin auth:** OIDC (provider-agnostic, default config example uses Pocket ID — matches Kursa)
- **Resource auth modes in v1:** `none`, `password`, `sso` (OIDC), `allowlist` (email + magic link)
- **Deferred:** PIN, API tokens, WebAuthn, per-path rules
- Limen signs JWTs with **Ed25519** (fast, small keys, well-supported in .NET 10)
- Ostiarius verifies locally (no round-trip per request — Pangolin's badger plugin calls Pangolin every request; we don't want that latency)
- **Revocation:** Ostiarius polls `/api/auth/revoked` every 30s for revoked JTIs → sub-minute revocation window

---

## 3. Protocol choices (important — these were revised after reference research)

Originally we planned gRPC everywhere. Research changed that.

| Link | Protocol | Why |
|------|----------|-----|
| Limen ↔ Limentinus (agent) | JSON over WebSocket, agent-initiated, persistent | Pangolin/Newt, Komodo, Portainer all converged on WS+JSON. Komodo's own code explicitly flagged binary protocol as tech debt. Easier to debug, no .proto toolchain, AOT-safe. |
| Limen ↔ Ostiarius | JSON over WebSocket | Same pattern as agents |
| Limen ↔ Forculus | HTTP REST | Gerbil (Pangolin's WG relay) uses this; "dumb relay" pattern — no need for bidi streams |

**Reconnection pattern (copied from Newt):** exponential backoff 1s → 2s → 5s → 15s → 30s → 60s. Config version counter for dedup. Chain-ID on commands to handle retries idempotently.

---

## 4. Reference projects studied

Cloned to `C:\GithubProjects\_research\<name>/`:

| Project | What we learned / copied | What we avoided |
|---------|--------------------------|-----------------|
| **Coolify** (PHP/Laravel) | Persistent deployment queue with status/logs/rollback; domain-organized actions; health check + auto-rollback | Their ApplicationDeploymentJob is 1000+ LoC god class — we decompose into explicit `DeployPipeline` stages. No registry auto-deploy in Coolify — real gap we fill. |
| **Komodo** (Rust) | Core/Periphery split, stateless Periphery, dual connection modes (dial in or be dialed) | Binary custom protocol (they flagged this as tech debt). Onboarding keys that never expire — we use short-lived TTL. |
| **Pangolin** (TS/Node) | Overall hub-and-spoke shape, resource-level auth concept (THE feature), provisioning key enrollment, WebSocket + JSON for agent comms | Dynamic build-time module swaps (complexity); badger plugin calls back to control plane every request (we do local Ed25519 verify instead) |
| **Gerbil** (Go, Pangolin's WG hub) | HTTP push + pull-on-boot, full-replacement boot + incremental runtime, mutex-protected WG device ops | No reconnect logic if Pangolin restarts — we add a 60s reconcile loop in Forculus |
| **Newt** (Go, Pangolin's agent) | Two-tier credentials (provisioning key → permanent ID+Secret), persistent WebSocket, userspace wireguard-go, chain-ID dedup | Config file at 0644 — we use 0600 |
| **Portainer** (Go) | EdgeKey composite token, ECDSA signed requests, heartbeat via polling | WebSocket only used for streaming (exec/logs) — we use WS for all control traffic since scale is smaller |

**Agent reports archived at:** `docs/research/` (see §7 below).

---

## 5. Organization & repo layout

```
github.com/getlimen/                     ← ORG NOT YET CREATED; user must create manually
├── limen            — central manager (C# + Angular + Postgres). Hosts Limen.Contracts NuGet source.
├── ostiarius        — reverse proxy (C# NativeAOT, YARP, LettuceEncrypt-Archon)
├── forculus         — WG hub (C# NativeAOT, `wg` CLI subprocess)
├── limentinus       — universal node agent (C# NativeAOT, wireguard-go userspace)
├── limen-cli        — admin CLI (v1.1+ deferred, repo scaffold only)
├── limen-docs       — docs site (later, repo scaffold only)
└── .github          — org-level templates + profile README
```

Each repo has:
- `LICENSE` (Apache 2.0)
- `README.md` (role description + install snippet when ready)
- `CLAUDE.md` (per-repo instructions for AI agents; links back to the central spec in `limen/docs/`)
- `.gitignore` (C# + Angular)
- `.editorconfig` (matching Kursa's conventions)

The full design spec and all plans live in `limen/docs/` since `limen` is the source-of-truth repo.

---

## 6. Implementation plan decomposition

The spec is decomposed into **7 sequential plans**. Each produces working, testable software on its own — you can stop at any plan and have something demoable.

| # | Plan | File | What it delivers |
|---|------|------|------------------|
| 1 | **Foundation** | `plan-01-foundation.md` | `limen` repo scaffolded; admin OIDC login works; empty Angular dashboard; CI builds + tests Docker image |
| 2 | **Agent control channel** | `plan-02-agent-control-channel.md` | `limentinus` enrolls via provisioning key; Limen sees agent in UI with heartbeat (no WG, no Docker) |
| 3 | **WireGuard tunnels** | `plan-03-wireguard-forculus.md` | All agent↔Limen traffic flows over WG; Forculus runs the hub; agents get tunnel IPs |
| 4 | **Ostiarius + public TLS** | `plan-04-ostiarius-proxy.md` | Admin adds a service + route; Ostiarius serves HTTPS with Let's Encrypt; no auth gate yet |
| 5 | **Docker deploys + auto-update** | `plan-05-docker-deploys.md` | Services deploy on `docker`-role nodes; registry polling triggers auto-redeploy; health-check rollback works |
| 6 | **Resource authentication** | `plan-06-resource-auth.md` | `password`/`sso`/`allowlist` modes; Ed25519 JWTs; Ostiarius AuthMiddleware; login + magic-link UI |
| 7 | **Polish** | `plan-07-polish-e2e.md` | Full E2E suite; per-repo READMEs; docs site scaffold; release pipeline |

**Recommended execution:** one plan at a time via the `superpowers:subagent-driven-development` skill (fresh subagent per task, two-stage review). Alternative: `superpowers:executing-plans` for batch inline execution.

---

## 7. What's already committed / where files live

Everything below is **local to `C:\GithubProjects\getlimen\`** — nothing is pushed yet (org doesn't exist).

```
C:\GithubProjects\getlimen\
├── limen\
│   ├── docs\
│   │   ├── superpowers\
│   │   │   ├── specs\
│   │   │   │   └── 2026-04-14-limen-design.md       ← authoritative design spec
│   │   │   └── plans\
│   │   │       ├── 2026-04-14-plan-01-foundation.md
│   │   │       ├── 2026-04-14-plan-02-agent-control-channel.md
│   │   │       ├── 2026-04-14-plan-03-wireguard-forculus.md
│   │   │       ├── 2026-04-14-plan-04-ostiarius-proxy.md
│   │   │       ├── 2026-04-14-plan-05-docker-deploys.md
│   │   │       ├── 2026-04-14-plan-06-resource-auth.md
│   │   │       └── 2026-04-14-plan-07-polish-e2e.md
│   │   ├── research\
│   │   │   └── 2026-04-14-reference-project-analysis.md  ← combined Coolify/Komodo/Pangolin/Gerbil/Newt/Portainer reports
│   │   ├── HANDOFF.md                                ← this file
│   │   └── roadmap.md                                ← v2+ features deferred
│   ├── contracts\Limen.Contracts\                    ← shared C# project (to be scaffolded in Plan 1)
│   ├── src\                                          ← to be scaffolded in Plan 1
│   ├── CLAUDE.md
│   ├── README.md
│   ├── LICENSE
│   ├── .gitignore
│   └── .editorconfig
├── ostiarius\          ← CLAUDE.md + README + LICENSE + gitignore + editorconfig
├── forculus\           ← same
├── limentinus\         ← same
├── limen-cli\          ← same
├── limen-docs\         ← same
└── .github\            ← org profile README + templates
```

Also: memory file at `C:\Users\nicla\.claude\projects\C--GithubProjects\memory\feedback_clean_architecture_layout.md` captures the clean-arch rules.

---

## 8. Open questions (to resolve during implementation)

Copied from §12 of the spec:

1. **JWT revocation storage growth** — In-memory revoked-JTI list fine for v1 scale; need pagination/expiry strategy beyond natural `exp` cleanup? Decide during Plan 6.
2. **Cookie SameSite** — `Lax` works for v1; is `Strict` ever better? Decide during Plan 6 auth flows.
3. **Registry credentials storage** — Column-level Postgres encryption with app-held key, or system keyring on control-plane host? Decide during Plan 5.
4. **`wg` vs `wg-quick` in Forculus** — `wg syncconf` is cleaner for reconciliation; test in Plan 3 prototype.
5. **Ostiarius public-key distribution** — Fetch on boot + poll periodically, or WS push on rotation? Decide during Plan 4 (most likely WS push with boot fallback).

---

## 9. What the user explicitly does NOT want

- Redis / Valkey / any non-Postgres data store
- MinIO / S3 / object storage
- Hangfire (LGPL + paid Pro)
- Building in-process proxy (Ostiarius is a separate container, not embedded in Limen)
- Full mesh VPN (hub-and-spoke only)
- Multi-admin in v1 (single admin only)
- Config-as-code in v1 (UI-only for defining services and routes)
- AI/Claude commit attribution

---

## 10. How to pick up work as a new agent

1. **Read this file.**
2. **Read the spec:** `docs/superpowers/specs/2026-04-14-limen-design.md`
3. **Read the next pending plan.** Start with `plan-01-foundation.md` if no work has started. Otherwise, identify the last completed plan by checking git history and what's been implemented.
4. **Check the user's clean-architecture rules** (§2.11 above — or the memory file at `C:\Users\nicla\.claude\projects\C--GithubProjects\memory\feedback_clean_architecture_layout.md`).
5. **Use `superpowers:subagent-driven-development`** to execute the next plan — fresh subagent per task, review between tasks.
6. **Never add AI/Claude attribution** to commits (see §2.12).

---

## 11. Glossary

| Term | Meaning |
|------|---------|
| **Control plane** | Limen itself — the management brain |
| **Data plane** | Ostiarius — handles actual user traffic |
| **Hub** | Forculus — the WG server-side endpoint |
| **Node** | Any host running Limentinus |
| **Role** | A capability flag on a node (`control`, `docker`, `proxy`) |
| **Service** | A Docker image + config an admin wants to run |
| **Route** | A public hostname mapping to a Service via a Proxy node |
| **Resource** | A public-facing thing Limen exposes (same as a Route conceptually, but when focusing on *what's protected behind auth*) |
| **Agent** | Limentinus running on a node |
| **Peer** | WireGuard peer — each agent is a peer of Forculus |

---

## 12. Credits / references

- **Brainstorming conducted with Claude via the `superpowers:brainstorming` skill** over a long session on 2026-04-14.
- **Reference projects studied:** Coolify, Komodo, Pangolin, Gerbil, Newt, Portainer (cloned to `C:\GithubProjects\_research\`).
- **User's existing project patterns studied:** Kursa (primary template), DockiUp (domain similarity), PangolinPatcher (Pangolin UI customization), PianUI (Angular template), ngx-m3-calendar (Material 3 components).
- All design decisions are captured in the spec; this handoff explains *why* each was chosen.
