# Limen — Decision Log

**Purpose:** Preserve the full reasoning behind every decision in chronological order, including discussions that were only summarized in `HANDOFF.md` and the design spec. A new agent reading this file understands not just *what* was decided but the actual debate, alternatives, and evidence that shaped each choice.

This file complements but does not replace:
- `docs/superpowers/specs/2026-04-14-limen-design.md` — authoritative current design
- `docs/HANDOFF.md` — distilled context for new agents
- `docs/research/2026-04-14-reference-project-analysis.md` — reference project analyses

---

## Table of contents

1. Naming journey
2. Scope framing (decomposition of the original ask)
3. Stack choices — the full reasoning
4. Background jobs: Hangfire → Quartz.NET (via TickerQ/Coravel consideration)
5. Reverse proxy deep dive: Caddy vs Traefik vs Nginx vs custom (Ostiarius)
6. ACME library: LettuceEncrypt → LettuceEncrypt-Archon
7. Database: Postgres-only (no Redis, no MinIO, no vector DB)
8. Enterprise / selling-to-big-corps discussion
9. Node role model evolution
10. Resource-level authentication design
11. Protocol pivot: gRPC → JSON over WebSocket
12. Ostiarius vs Vestibulum head-to-head (full GitHub conflict details)
13. Forculus naming (Portunus rejection details)
14. Component daemon naming (Cardea rejection details)
15. Clean architecture rule refinement (mid-brainstorm correction)
16. Plan decomposition rationale (why 7 plans)
17. Kubernetes — on the roadmap since the start

---

## 1. Naming journey

### Starting point

The project was originally described by the user as `UDKRPAPM` — "unified Docker kubernetes Reverse-Proxy and project management." An acronym, unusable as a brand.

First decision: pick a proper name before anything else.

### Names considered and rejected

All names went through GitHub search + web research before being accepted or rejected.

| Name | Origin / meaning | Result | Reason |
|------|------------------|--------|--------|
| **Helmsman** | Steers a ship — pun on K8s Helm + Docker "container ship" | ❌ rejected | Exists as a Helm-charts CLI tool. Direct ecosystem conflict. |
| **Bastion** | Fortified gateway | ❌ rejected | **Miles0sage/bastion** is an actively-developed *Self-hosted Cloudflare replacement with reverse proxy, auto-SSL, WireGuard tunnels, rate limiting, real-time dashboard — all in ~1,200 lines of Go.* Near-identical to Limen's premise. Plus `cloudposse/bastion` (Docker bastion SSH host). Dealbreaker. |
| **Portway** | Port + gateway | ⚠️ didn't pursue after Bastion rejection | Too similar in vibe to other networking names |
| **Anchorage** | Harbor / docking hub | ⚠️ mentioned, not deep-researched | Longer, less punchy than alternatives |
| **Conduit** | Tunnel / proxy | ⚠️ mentioned, not deep-researched | Generic |
| **Keelstone** | Ship's foundation | ⚠️ mentioned, not deep-researched | Obscure metaphor |
| **Janus** | Roman god of gates, doorways, transitions | ❌ rejected after brief acceptance | **HelloFresh's Janus** is literally *"An API Gateway"* with Docker install guide — direct reverse-proxy overlap. **Janus (Meetecho)** is a WebRTC gateway — major brand in streaming. **Janus (ACM 2022 paper)** is literally *"Lightweight Container Orchestration"* with centralized controller managing Docker nodes — almost identical concept. **Janus v1.0** on OSTI.GOV is yet another project. Four concurrent uses in adjacent spaces = unwinnable SEO fight. |
| **Portus** | Latin for harbor / network port | ❌ rejected | SUSE had a Portus registry frontend (now archived but brand baggage in registry space). |
| **Pharos** | Ancient lighthouse of Alexandria | ❌ rejected | **Kontena Pharos** is a Kubernetes distribution. **Lob Pharos** is a K8s cluster discovery tool. Both directly in our space. |
| **Terminus** | Roman god of boundaries | ❌ rejected | TerminusDB (3.2k ⭐), godaddy/terminus (1.9k ⭐), Pantheon's terminus CLI (336 ⭐), Sublime Terminus (1.4k ⭐). Drowning in namespace. |
| **Iris** | Greek messenger goddess | ❌ not pursued for central name | `irislib/iris-messenger` (734 ⭐) is a direct messenger conflict |
| **Aegis** | Zeus's shield | ⚠️ mentioned, too common in security tools | |
| **Limen** | Latin: "threshold" — the stone at the bottom of a Roman doorway | ✅ **ACCEPTED** | Prior art: Python DRM library (5 ⭐), a JS stock tool (7 ⭐), a VHDL processor (12 ⭐), a UEFI bootkit (7 ⭐) — all off-topic, none in our space. Clean on GitHub, zero conflicts in self-hosted/infra/proxy categories. |

### Why Limen (in the user's own decision)

Three reasons locked it in:
1. **Semantic precision** — Limen literally names what the tool does. Every packet, request, and deploy crosses a threshold it controls. Better than a vague "gateway" or "platform" name.
2. **No GitHub namespace collision** — searches returned only unrelated projects. Future brand building is unblocked.
3. **Roman doorway pantheon expansion** — the Romans had *named deities for every part of a door* (Forculus, Cardea, Limentinus, Ostiarius, Janus, Portunus, etc.). This gave a rich, internally-coherent naming system for the rest of the components without resorting to arbitrary brand names.

### Organization handle

`getlimen` chosen after discovering:
- `limen` on GitHub — taken
- `limen-io` — taken
- `limendev` — free
- `limen-dev` — free
- `getlimen` — free ✅

`getlimen` follows the established OSS convention: getsentry, getmeili, getlago, getoutline, getconvex. Picked because it's recognized pattern; brand becomes "Limen" in docs, "getlimen" only in GitHub handle.

---

## 2. Scope framing

### The original request contained 5 subsystems

User's first message described:
1. Docker orchestrator
2. Kubernetes orchestrator
3. Reverse proxy (Coolify/Pangolin-like)
4. WireGuard mesh VPN
5. Project management UI (Angular)

Plus: "clone koolify or coolify komodo pangolin and pangolins wireguard fork as i need this."

### Initial scope challenge

This is effectively "Coolify + Pangolin + a K8s controller in one codebase." That's 5 independent subsystems, any one of which is a multi-year project. Coolify alone (~50k ⭐, dozens of contributors) still doesn't do K8s + mesh VPN. Building all five from scratch in C# would be >5 years of work before anything ships.

Three paths were presented:

**(a) Minimum differentiating slice for v1**
Pick the 1-2 most-differentiating subsystems, ship them well, layer the rest later.
*Suggested v1 slice: WireGuard + reverse proxy + Docker deploy — the "Pangolin+Coolify-lite" combo. K8s deferred.*

**(b) Thin slice across all 5**
Skeleton each subsystem with minimal features, prove they integrate end-to-end, deepen iteratively.
*Higher risk, more impressive demo, lots of half-built code.*

**(c) Wrap existing tools (Coolify + Pangolin)**
Limen becomes a *unifying control plane* over Coolify + WireGuard daemon + k3s. Integration layer + UX only.
*Rejected quickly: Coolify is PHP/Laravel — wrapping it from C# is awkward and brittle.*

### User's choice

**(a)** — minimum slice in C#/Angular — **with note**: keep (b) in mind in planning documents so v2+ vision stays visible.

### Further clarification during brainstorm

User later clarified: the "project management" he originally mentioned is **not** a Linear/Jira-style feature. It's the deploy-management UX itself (auto-deploy on git push, manage Docker stacks, monitor containers) — i.e., what Coolify/Komodo provide, not a separate ticketing subsystem.

This simplified the v1 scope to effectively:
- Docker deploy management (was already in scope)
- Reverse proxy (was already in scope)
- WireGuard hub-and-spoke (was already in scope)
- Angular UI (was always implicit)

K8s remained deferred to v0.3.0. "PM" as a separate subsystem vanished from scope.

---

## 3. Stack choices — the full reasoning

### Tech stack inherited from Kursa

User's most-recent C# project (`PianoNic/Kursa` — an AI-powered LMS) has a rich CLAUDE.md declaring:
- .NET 10 / ASP.NET Core
- Angular 21 + spartan.ng + Tailwind CSS 4
- EF Core code-first
- PostgreSQL
- Redis (cache)
- Qdrant (vector DB)
- MinIO (object storage)
- Hangfire (background jobs)
- OIDC (Pocket ID)
- CQRS + source-generated Mediator
- Docker + docker-compose

### Limen deltas from Kursa

User explicitly narrowed the stack during brainstorm:

> *"do not use ANY other db than postgres so no redis and no object storage. also for angfire doesnt asp.net have already a integrated one?"*

This produced three deltas:

1. **No Redis** — replaced by `IMemoryCache` (in-process) + Postgres `LISTEN/NOTIFY` if pub/sub ever needed.
2. **No MinIO** — replaced by local Docker volume for small artifacts (WireGuard keys, cert cache, audit logs).
3. **No Qdrant** — not needed since Limen has no AI/RAG features.
4. **Hangfire → question asked** — user correctly noted ASP.NET has `IHostedService`/`BackgroundService` built-in. Full discussion below.

### Why Postgres-only

Three reasons made this easy:
- Single-admin, single-host deployment is the primary target. No clustering pressure.
- Every datum Limen stores (nodes, services, routes, deployments, sessions) has relational constraints. There's no "unstructured blob" workload.
- Operational simplicity. One container to back up. One migration system. One connection pool.

Postgres replaces:
- Redis — via `IMemoryCache` (single-process cache) and `LISTEN/NOTIFY` (if needed)
- Object storage — via Docker volume mounts (small files only; cert data, WG configs)
- Vector DB — doesn't apply here

### Angular 21 + spartan.ng + Tailwind 4

Kept as-is from Kursa. spartan.ng is shadcn-for-Angular; the brain/helm component pattern matches modern Angular idioms. Tailwind 4 is current stable.

### ASP.NET Core with Minimal APIs or Controllers

Per Kursa CLAUDE.md — pick per-endpoint depending on complexity. For Limen that means:
- Minimal APIs for most thin endpoints (health, auth, CRUD)
- Controllers only if an endpoint has substantial middleware/filter coupling (none expected in v1)

---

## 4. Background jobs: Hangfire → Quartz.NET

### The user's first challenge

> *"is therre a lighweiger more poulare ting than Hangfire? eversthing should be mit licensed or somehting wich is not comercialy locked"*

This triggered a proper library comparison. Key finding: **Hangfire core is LGPL-v3, with paid Pro extensions**. That's not fully permissive, and "commercial lock" exists at the Pro tier.

### Alternatives researched (2026 landscape)

| Library | License | Persistence | Dashboard | Notes |
|---------|---------|-------------|-----------|-------|
| **Coravel** | MIT ✅ | In-memory (DIY persist) | None | Very fluent API; tiny; `jamesmh/coravel` on GitHub (~3.7k ⭐). Great for simple scheduling but no Postgres-backed persistence out of the box. |
| **NCronJob** | MIT ✅ | In-memory | None | Modern minimalist cron library. Growing fast. Similar to Coravel but even tighter focus on cron triggers. |
| **TickerQ** | MIT ✅ | **Postgres/EF** ✅ | **SignalR-powered live UI** ✅ | Newer but interesting: reflection-free design, persistence + UI built-in. |
| **Quartz.NET** | Apache 2.0 ✅ | Postgres ✅ (AdoJobStore) | None (3rd-party dashboards exist) | 17-year mature, used everywhere. Built-in retry/cron/clustering. Heavier setup ceremony than Coravel. |
| **Hangfire** | **LGPL v3 ⚠️** core + paid Pro | Postgres ✅ | Yes ✅ | Battle-tested in .NET ecosystem, but license + paid tier were disqualifiers. |

### First direction: TickerQ

TickerQ looked attractive because it was the only MIT option with Postgres persistence AND a dashboard. But then the user asked a sharper question:

> *"quck question, why do we even need a dasbhourd for this?"*

### The "no dashboard" realization

This was a clean YAGNI moment. The jobs Limen will schedule are:
- Poll Docker registry for new image versions
- Pull image when update detected
- Restart container on update
- Periodic health checks
- Cert renewal scans
- Cleanup jobs

An admin looking at Limen wants to see *deploy status* and *service health* — things Limen's main Angular UI surfaces as first-class features. They don't care about "internal cron tick #4732." A dedicated jobs dashboard is **debugging convenience for the dev (Niclas), not a feature for the admin user**.

Decision: drop "dashboard" from the requirements. Real requirements become:
1. Persistence (scheduled deploys must survive restarts)
2. Retry policy
3. Cron support
4. Permissive license

### Why Quartz.NET won

With dashboard off the table, the comparison narrowed:

| Criterion | Quartz.NET | Coravel |
|-----------|-----------|---------|
| License | Apache 2.0 ✅ | MIT ✅ |
| Persistence in Postgres | ✅ Built-in (AdoJobStore) | ❌ You'd build it |
| Retries | ✅ Built-in | ❌ DIY in job code |
| Cron | ✅ Native | ✅ Native |
| Maturity | 17+ years, used everywhere | ~7 years, smaller |
| Setup ceremony | Medium | Tiny |

Quartz provided both persistence + retries out of the box. The "heavy ceremony" reputation is mostly old XML-config memory — modern fluent DI registration is clean.

### Final Quartz license verification

User asked one final follow-up: *"can you search online if this need any pricing"*. Search confirmed:

- Apache 2.0
- **No Pro tier, no Enterprise tier**
- **No usage fees, no paid features**
- Every feature is in the OSS core
- **QuartzDesk** is a separate unrelated paid product (monitoring for Quartz — not required, ignore)

Verdict: **Quartz.NET, Apache 2.0, free forever.** Locked in.

---

## 5. Reverse proxy deep dive

### First proposal: Caddy sidecar

Initial design had Caddy as a separate container, configured programmatically by Limen via its JSON Admin API. This followed the Coolify/Pangolin/Dokploy pattern (external proxy container, programmatic config, auto-HTTPS).

### User question: How good/robust is Caddy?

Triggered a 2026 robustness comparison. Summary of findings from contemporary sources:

**Caddy's stability issues — surfaced:**

| Version | Known memory issue |
|---------|--------------------|
| 2.2.1 | Memory leak using file_server (later classified as high usage not leak) |
| 2.6.3 | High memory during Docker image builds in CI — fixed by downgrade to 2.6.2 |
| 2.7.x | Reports of constant memory leaks in minimal configs |
| 2.9 | Release notes: "refinements and bug fixes in many areas" — no specific "memory leak fixed" headline |
| 2.10.2 / 2.11.2 | Current stable. Caddy team published a *Profiling* guide specifically to help users diagnose their own leaks — implicit acknowledgment that this is an ongoing concern. Nov 2025 issue #7350: "sporadic RAM spikes in Docker causing OOM kills." |

**Memory profile comparison (idle → under load):**

| Proxy | Idle RAM | Under heavy load | Notes |
|-------|----------|------------------|-------|
| Nginx | ~5–20 MB | Lowest (C, optimized) | Most-deployed proxy on earth |
| Caddy | ~50–150 MB | Known OOM events at scale | Go runtime + JSON config tree in memory |
| Traefik | ~100–200 MB | Higher than Caddy but more predictable | Maintains Docker provider state + routing table in memory |

**Honest verdict on Caddy for Limen's scale:**
- The memory leak reports cluster around **very high traffic** (thousands of concurrent users on small VMs) and **specific configurations** (Cloudflare upstream)
- Limen's realistic scale is an admin's self-hosted infra: ~5-50 services, <100 req/s peak, running in a container with memory limits
- At that scale Caddy is fine; at SaaS scale Caddy is known to be temperamental
- Docker container + memory limits + auto-restart = even if a leak hits, Docker restarts Caddy

### User pivot question: "what if i want to sell this to big corps?"

Triggered a deeper enterprise analysis. The trade-off table that came out:

| Decision | Self-hosted homelab | Big-corp customers |
|----------|---------------------|--------------------|
| **Reverse proxy** | Caddy fine | **Traefik** — has Traefik Enterprise (paid SLA), proven at scale, what big corps already deploy |
| **Job runner** | Quartz.NET fine | Quartz still good, **but Hangfire's dashboard becomes valuable** — enterprise ops teams expect a job UI for audit |
| **Single Postgres** | Perfect | ⚠️ Many enterprises mandate caching layer (Redis), HA databases. They'll want options. |
| **Single admin** | Perfect | ❌ Won't fly. Need RBAC, SSO (SAML/OIDC), audit logs |
| **Auth** | Pocket ID / single password | **SAML + OIDC + LDAP** — corps require IdP integration (Okta, Entra ID, Keycloak) |
| **Multi-tenancy** | None needed | Required if SaaS, optional if on-prem |
| **License** | MIT/Apache fine | **Open-core** or **BSL** (Business Source License like HashiCorp/Sentry/Codecov) — keeps community version free, monetizes enterprise features |
| **Support model** | None | Paid support contracts, SLAs, dedicated channels |
| **Audit/compliance** | N/A | SOC 2, ISO 27001 eventually |

### Enterprise path — four modes considered

1. **On-premise per-seat licensing** — they install Limen on their infra, you charge license/year
2. **Self-hosted + paid support** — Limen is OSS (community), big corps pay for support contracts and enterprise features (open-core, like GitLab/Sentry/Coolify Pro)
3. **Hosted SaaS** — you operate Limen for them, they pay subscription. Requires multi-tenancy + compliance burden.
4. **Build now for selling later** — keep architectural doors open without committing

### Enterprise verdict

**Don't try to be enterprise-grade in v1.** That's a 5-10× scope multiplier (RBAC, SSO, audit logs, HA, compliance, multi-tenancy, support tooling). Would never ship.

Instead:
1. **v1:** self-hosted homelab tool. Single admin, Postgres only, MIT/Apache. Get it working and loved by 100 self-hosters.
2. **v2:** features people ask for (multi-admin, OIDC SSO, RBAC)
3. **v3 (if traction is real):** open-core split. Community edition stays Apache-2.0. Enterprise edition gets paid features (SAML, audit logs, HA, support) under BSL or similar.

This is captured in `docs/roadmap.md` as v2.0 "Enterprise edition" entry.

**One decision changed based on enterprise thinking:** keep enterprise on the roadmap ≠ build for enterprise now. **Only suggested change: pick Traefik over Caddy for v1** since Traefik is what big corps already trust.

### User's response: "still, use caddy please"

Decision locked: **Caddy** with JSON Config API. Enterprise considerations noted in `docs/roadmap.md` for v2/v3. Caddy is fine for Limen's actual target scale.

### The pivot to custom Ostiarius

Shortly after locking Caddy, the user reframed:

> *"no no i thougt of makeing my own image then just like caddy but it is speficcifly for limen not a caddy alternative"*

This was a significant architectural shift. Instead of orchestrating a third-party proxy (Caddy sidecar pattern), Limen would ship its own reverse-proxy binary — **Ostiarius** — built on YARP + LettuceEncrypt-Archon.

**Trade-offs:**

| Aspect | Custom Ostiarius | Caddy sidecar |
|--------|------------------|----------------|
| Container count | 1 less (Ostiarius + Limentinus co-exist as a proxy node's full stack) | Separate Caddy container |
| Stack uniformity | ✅ Pure C# everywhere | Caddy = Go binary |
| Debugging | ✅ One process, one log stream | Two surfaces |
| Failure isolation | Ostiarius crash = ingress dies (but so does Limentinus' role supervisor) | Caddy survives Limen crashes |
| Memory pressure | All proxied traffic through Ostiarius C# process | Caddy handles its own load |
| Battle-testing as edge proxy | YARP is good but younger than Caddy | Caddy is mature at edge |
| Programmatic config | Native C# objects + in-memory YARP IProxyConfigProvider | JSON over Caddy admin API |
| HTTP/3 maturity | Good (YARP 2026) | Excellent (Caddy default) |

### Honest callout kept in spec

The design spec and handoff both flag this as **the biggest architectural risk in v1**:
> *"every reference project uses Traefik. Pangolin, Coolify, Portainer, Komodo all use Traefik indirectly. We diverge here for stack uniformity. If Ostiarius slips, falling back to Traefik as temporary data plane is always an option."*

### Why the divergence was accepted

User's reasons:
- Uniform C# stack makes the project easier to maintain solo
- Single language = single build + debug + profiling flow
- Limen's programmatic config needs (real-time WS push from Limen to proxy) fit YARP's `InMemoryConfigProvider` pattern perfectly
- Writing ~500-1000 LoC of YARP wiring is not much more work than writing a Caddy JSON API client

---

## 6. ACME library: LettuceEncrypt → LettuceEncrypt-Archon

### First choice: LettuceEncrypt

Default choice for ACME on Kestrel/ASP.NET Core. Created by Nate McMaster (ex-Microsoft). Widely used. Simple API:

```csharp
services.AddLettuceEncrypt(opts => {
    opts.AcceptTermsOfService = true;
    opts.DomainNames = new[] { "app.example.com" };
    opts.EmailAddress = "admin@example.com";
});
```

### User correction: "LettuceEncrypt is debricated please search for a alternative"

Triggered research into the .NET ACME-on-Kestrel landscape.

### 2026 landscape findings

| Option | Maintenance | Verdict |
|--------|-------------|---------|
| **LettuceEncrypt** (natemcmaster) | *"The creator has lost interest in developing features but willing to patch security issues."* NuGet package described as *"archived and no longer supported"*; Microsoft docs no longer recommend it. | ❌ Dead end |
| **LettuceEncrypt-Archon** | Community fork; active commits; same API; drop-in replacement | ✅ Active fork |
| **Certes** (fszlin) | Pure ACME v2 client library (still maintained, slow cadence). Last release Jan 2023 — also slowing. You'd write cert acquisition + storage + renewal scheduling yourself. | ⚠️ Library maintained, you own pipeline |
| **ACMESharpCore** (PKISharp) | Port of ACMESharp to .NET Standard | ⚠️ Library maintained, you own pipeline |
| **Certbot sidecar** | Battle-tested by millions. Shared volume with Ostiarius, file-watcher for cert reload | ✅ Works, architectural ugliness |

### Options presented

1. **LettuceEncrypt-Archon** — ~5 LoC change from original LettuceEncrypt
2. **Certes + DIY Kestrel wiring** — ~200-400 LoC; full control; supports DNS-01 (wildcard certs)
3. **Certbot sidecar** — standard `certbot/certbot` Docker image renewing into shared volume; Ostiarius reads from volume

### User's choice: "just use the lettuceEncrypt-Archon"

Locked in. Lowest-effort, drop-in. If the Archon fork also stagnates in the future, migration path is clear (small code surface, well-bounded).

---

## 7. Database: Postgres-only

### User's explicit constraint

> *"do not use ANY other db than postgres so no redis and no object storage."*

### What was replaced

| Was planned (Kursa pattern) | Replaced with | Reason |
|-----------------------------|---------------|--------|
| Redis (cache) | `IMemoryCache` (in-process) | Limen is single-node in v1; no need for a distributed cache. IMemoryCache covers admin session, route table, cert metadata. |
| Redis (pub/sub, for SignalR) | Postgres `LISTEN/NOTIFY` if ever needed | Npgsql supports it natively. Avoids second broker. |
| MinIO (object storage) | Docker volume mount | Small artifacts only (certs, WG configs). Filesystem is fine. |
| Qdrant (vector DB) | Not needed | No AI/RAG features in Limen |

### Operational simplicity win

One datastore means:
- `pg_dump` = backup
- One connection pool
- One migration system (EF Core)
- Admin doesn't need to understand Redis/MinIO/Qdrant operations
- Container count: Limen + Postgres + Forculus + Ostiarius (4 containers) — add Redis/MinIO/Qdrant and it's 7

---

## 8. Enterprise / selling-to-big-corps discussion

Full text of the analysis is above in §5 (Reverse proxy deep dive → "User pivot question").

### Decisions that came from this discussion

| Area | v1 (homelab) | Enterprise roadmap |
|------|---------------|---------------------|
| Admin count | 1 | Multi-admin + RBAC — v0.2.0 |
| SSO | OIDC (any provider) | + SAML, + LDAP — v2.0 enterprise tier |
| Audit logs | None | Required — v2.0 enterprise tier |
| HA control plane | None | Postgres leader election — v1.1+ |
| Multi-tenancy | None | Out of scope for on-prem; required if hosted SaaS (not shipping SaaS) |
| License | Apache 2.0 | Open-core split: community stays Apache, enterprise uses BSL |
| Support model | Community only | Paid support contracts — v2.0 |
| Compliance (SOC 2 / ISO 27001) | N/A | v2.0+ |

### License-choice reasoning

**MIT vs Apache 2.0 vs BSL for v1:**
- **MIT** — simplest, permissive
- **Apache 2.0** ← **chosen** — same permissiveness as MIT + explicit patent grant (protects contributors and users) + enterprise-friendly legal language
- **BSL** — eventually-OSS, blocks SaaS competitors from hosting. HashiCorp/Sentry style. Adds adoption friction at homelab stage. Deferred to v2.0 enterprise edition.

Apache 2.0 also allows us to dual-license later if we go open-core without disrupting existing users.

---

## 9. Node role model evolution

### Initial topology

Draft 1 had two distinct node types:
- **Central node** — runs Limen + Forculus + Postgres (control plane). Singular.
- **Edge nodes** — run Limentinus (the agent). Plural.

This was too rigid. Users might want:
- A dedicated edge proxy node (no Docker workload)
- A compute-only node (no public IP)
- An all-in-one node (docker + proxy + control on one machine)

### User refinement round 1

> *"can we make so there is a other node type wich can also act as eather only docker or reverse proxy or both?"*

This introduced **role flags** — every agent is a single binary, but admin picks its capabilities at install time.

```yaml
# edge-proxy node (no Docker, just TLS ingress)
LIMEN_ROLES=proxy

# compute-only node
LIMEN_ROLES=docker

# combined
LIMEN_ROLES=docker,proxy
```

### User refinement round 2

> *"but also the controll node can have docker and or reverse proxy"*

This collapsed the model further. No special "control node type" — just role flags all the way.

```yaml
# single-node homelab — everything on one machine
LIMEN_ROLES=control,docker,proxy

# dedicated control plane
LIMEN_ROLES=control

# HA-adjacent: two control nodes (deferred, v1.1+)
# ...
```

### User refinement round 3

> *"or maby on the host where he also wants to host something there has to be a node installed if this makes sense"*

This was the user working through the mental model. Correct: **a host becomes a node at the moment Limentinus is installed on it.** Before that, Limen has no visibility or control. Standard agent pattern — identical to Pangolin, Coolify, Komodo, Portainer's agent mode.

### Final model (captured in spec §4.2)

- Every host = generic *host*
- Every *host* + Limentinus running on it = *node*
- Node has role flags; roles determine which additional containers Limentinus brings up
- `control` role = Limen + Forculus + Postgres
- `proxy` role = Ostiarius
- `docker` role = Docker socket access for user services

Any combination is valid. Typical v1 is `control,docker,proxy` all on one machine (homelab).

---

## 10. Resource-level authentication design

### How the requirement surfaced

After Sections 1-5 of the design were agreed, user asked:

> *"okay quick question, there is one feature left wich is very important in pangolin, can you guess what?"*

My guess (correct): **resource-level authentication** — Pangolin's killer feature where it puts a login wall (SSO / password / PIN / email allowlist) in front of any exposed resource, so a backend app that has zero authentication of its own suddenly requires login. The "badger" Traefik plugin calls back to Pangolin for auth decisions on every request.

### Pangolin's implementation (noted for reference)

- **Auth modes:** SSO, password, PIN code, email whitelist, API token
- **Architecture:** Traefik + `badger` plugin → HTTP callback to Pangolin for every request
- **Session model:** cookies, multi-tenant, per-user accounts

### What Limen adopts vs. changes

| Feature | Pangolin | Limen |
|---------|----------|-------|
| Auth wall concept | Yes | ✅ Yes |
| Check on every request | Call back to control plane | **Local verify in Ostiarius** (Ed25519 JWT) — no round-trip |
| User accounts | Yes, multi-tenant | None in v1 — credentials are per-resource (not "accounts") |
| Auth modes v1 | SSO, password, PIN, allowlist, API token | `none`, `password`, `sso`, `allowlist` (PIN/API token deferred to v0.5.0) |
| Cookie scoping | Per-resource or apex | Same (configurable `strict`/`domain`) |
| Header injection | Yes (X-Pangolin-*) | Yes (X-Limen-User-Id, X-Limen-User-Email, X-Limen-Auth-Method, X-Limen-Resource-Id) |
| Revocation | Check on each request | **30s polling** of revoked-JTI list — sub-minute revocation window |

### Why local verification (not per-request callback)

Pangolin's badger-plugin-calls-Pangolin pattern means:
- Every single request to a protected resource adds a round-trip to the control plane
- Latency spikes if Pangolin is under load
- Pangolin becomes a hot path — can't restart without dropping auth for every in-flight request

Limen's Ed25519 + 15-min JWT + 30s revocation poll means:
- Ostiarius verifies locally (~50 µs Ed25519 verify)
- Only login flows hit Limen
- Limen can restart; in-flight sessions are unaffected
- Cost: up to 30s revocation window (acceptable trade for simplicity)

### Full flows documented in spec §7.5 + §7.6

And in plan 06 (Resource auth). Included here for completeness of the decision log.

---

## 11. Protocol pivot: gRPC → JSON over WebSocket

### Initial design used gRPC

Original plan had:
- Limen ↔ Limentinus: gRPC bidirectional stream
- Limen ↔ Ostiarius: gRPC bidirectional stream
- Limen ↔ Forculus: gRPC

Rationale: type safety across services via proto contracts, tight framing, well-supported in .NET.

### What research changed

User asked me to research how Pangolin/Coolify/Komodo/Gerbil/Newt/Portainer actually implement their agent protocols. The results (archived in `docs/research/2026-04-14-reference-project-analysis.md`):

- **Pangolin/Newt:** WebSocket + JSON, persistent, agent-initiated
- **Komodo:** WebSocket with custom binary framing — *Komodo's own code explicitly flagged this as tech debt*, recommending JSON over WebSocket instead
- **Portainer:** HTTP polling as primary; WebSocket only for streaming (exec/logs)
- **Gerbil:** HTTP REST only (push from Pangolin, pull on boot)

Nobody uses gRPC for this pattern. The convergence on WebSocket + JSON had multiple strong reasons:
1. Browser dev tools can inspect JSON over WebSocket
2. No .proto toolchain required
3. AOT-safe without special reflection handling
4. Easy to version manually (drop a field, add one)
5. Trivially debuggable by tailing a log

### Decision

- **Limen ↔ Limentinus:** JSON over WebSocket, agent-initiated, auto-reconnect (1s→60s backoff)
- **Limen ↔ Ostiarius:** JSON over WebSocket (same pattern)
- **Limen ↔ Forculus:** HTTP REST (matching Gerbil's "dumb relay" pattern — no need for bidi stream)

Replaces earlier gRPC plan. Shared DTOs live in `Limen.Contracts` as C# records, serialized via `System.Text.Json` source generators (AOT-safe).

---

## 12. Ostiarius vs Vestibulum head-to-head

After locking **Limen** (center), **Forculus** (WG hub), **Limentinus** (agent), we needed a name for the **reverse proxy** component. Two finalists came out of the Roman doorway pantheon:

### Candidate: Ostiarius

**Meaning:** Doorkeeper / porter. The Roman slave whose job was to stand at the door, inspect visitors, and decide who comes in.

**Semantic fit:** Perfect. A reverse proxy *inspects* requests (TLS handshake, headers), *decides* whether they pass (auth), *routes* them (YARP). That's literally the doorkeeper's job.

**GitHub conflict check:**

| Repo | Stars | Language | What it is |
|------|-------|----------|------------|
| `objective-see/Ostiarius` | 10 | Objective-C | macOS app blocking unsigned binaries (El Capitan era, dormant since ~2015) |
| `elebihan/ostiarius` | 0 | Rust | *"Simple centralized command execution management"* |
| `EntryDSM/Ostiarius` | 1 | Python | *"Simple API Gateway for EntryDSM backend"* — **closest semantic conflict, but 1-star from a student org, dormant** |
| `Air-Light-Time-Space/ostiarius` | 0 | — | Access-control system for shared space |

User handle `Ostiarius` is taken (a personal account from 2018, 3 repos, 0 followers, effectively dormant — doesn't matter for us since we use `getlimen/ostiarius`).

**Verdict:** one minor active-ish conflict (EntryDSM's API Gateway), but 1 star and clearly tiny. Workable.

### Candidate: Vestibulum

**Meaning:** Roman entryway / antechamber — the room visitors enter first.

**Semantic fit:** Descriptive but passive. A reverse proxy *does things* (inspect/route/authenticate); a vestibulum just *is* a location.

**GitHub conflict check:**

| Repo | Stars | Language | What it is |
|------|-------|----------|------------|
| `Sigmo4ka/vestibulum2` | 0 | PHP | (school project) |
| `rsm198507/vestibulum` | 0 | JS | (personal) |
| `marciaibanez/vestibulum` | 1 | HTML | (personal) |
| `lins-dev/vestibulum` | 0 | PHP | (personal) |
| `JagodaK/vestibulum` | 0 | CSS | (personal) |
| `wallamejorge/ADSD_ProyectoFinal_VestibulumSensorium` | 1 | C | (school, 2013) |
| `ciptard/vestibulum` | 0 | (none) | *"deadly simple flat-file Markdown CMS"* — slight conceptual conflict, dormant |
| `ZiruZanrgeiff/Vestibulum` | 0 | JS | (personal) |

**Cultural context:** *vestibular* in Portuguese/Spanish-speaking countries = the university entrance exam. Many Brazilian student-project repos use the word. Creates marginal market noise there but English-speaking dev community won't trip on it.

User org handle `Vestibulum` exists as an empty placeholder from 2014 with 0 repos and 0 followers.

**Verdict:** cleaner on GitHub (no active conflicts with any overlap in our space) but less semantically accurate.

### Side-by-side

| Criterion | Ostiarius | Vestibulum |
|-----------|-----------|------------|
| GitHub conflicts | ⚠️ One 1-star API-gateway conflict | ✅ All trivial school projects, no overlap |
| Semantic accuracy | ✅ Exact role match ("the doorkeeper who inspects") | ⚠️ "Entryway" — passive, a place |
| Length | 9 chars | 10 chars |
| Pronunciation | os-tee-AR-ee-us (5 syllables) | ves-TIB-yoo-lum (4 syllables) |
| Cultural noise | None | Portuguese "vestibular" collision |
| Role vibe | Active (a person) | Passive (a place) |
| Naming theme cohesion | ✅ Fits with Limen + Forculus + Limentinus (all deities/roles) | ⚠️ Place name doesn't match the deity pattern |

### Decision

**Ostiarius** — for these reasons:
1. The EntryDSM conflict is dormant and 1-star. Not a serious prior art concern.
2. Semantic precision matters more than marginal namespace cleanliness.
3. The four-deity pantheon (Limen + Ostiarius + Forculus + Limentinus) reads as cohesive Roman doorway theology; Vestibulum is a *place*, breaking the pattern.

Locked in. Repo is `getlimen/ostiarius`.

---

## 13. Forculus naming (Portunus rejection details)

Full rationale for rejecting Portunus (the first candidate for the WG hub):

### Candidate: Portunus

**Meaning:** Roman god of doors AND harbors/ports. Mariners' patron. Festival was the Portunalia, where keys were thrown into fires for protection. Double meaning (network port + harbor) was initially attractive.

**GitHub conflict check — fatal:**

| Repo | Stars | Language | What it is |
|------|-------|----------|------------|
| `majewsky/portunus` | 90 | Go | *"Self-contained user/group management and authentication service"* (LDAP-style auth tool) |
| `IQTLabs/portunus` | 11 | Python | *"multi-tenant environments to run experiments"* |
| `keezel-co/portunus_documentation` | 8 | — | Documentation for Portunus project |
| `keezel-co/portunus_docker` | 3 | Shell | *"Cablegaurd all-in-one docker execution"* |
| `keezel-co/portunus_provisioner` | 0 | Python | *"Cableguard remote server provisioner"* |
| `keezel-co/portunus_cd` | 2 | Python | *"Cableguard Config Deliverer - **Wireguard provisioning tool**"* |
| `jaffreyjoy/portunus` | 8 | Vue | File-storage with brain-biometric auth |
| `Ian-MacLeod/portunus-old` | 0 | — | Self-hosted single sign-on |
| `andrei-pavel/portunus` | 13 | Shell | Package-manager aggregator |
| `mrdmnd/portunus` | 7 | Lua | WoW tool |

**Dealbreaker:** `keezel-co` has **multiple repos using Portunus as their brand for WireGuard provisioning** (the Cableguard project). That's a direct domain overlap with what Forculus does. Choosing Portunus for a WireGuard hub = brand collision with an existing WG provisioning tool. Hard pass.

### Forculus accepted instead

**Meaning:** Roman god of the door panel. Ovid and Augustine both record him as one of the minor deities Romans invoked for specific parts of a door. Forculus guards the solid structure of the gate itself.

**GitHub check:**

| Repo | Stars | Language | What it is |
|------|-------|----------|------------|
| `zinic/forculus` | 0 | Go | Zoneminder automation |
| `mailuminatti/forculus` | 0 | Python | (personal) |
| `Koninklijke-van-Twist/Forculus` | 0 | PHP | Dutch sleutelbeheer (key management) webapp |
| `mgadd02/Forculus-Gamboge` | 0 | C | School project |
| `yavuzCodiin/VAftpRAPC` | 0 | Python | Physical door-access system |

All 0-star. Effectively clean. Locked in as the WG hub name.

### Bonus alternative considered: Limentinus for hub, Forculus for agent

Briefly considered swapping — Limentinus ("guardian of the threshold") fits the WG hub (protects the whole network from outside) and Forculus (the door panel) fits the agent (the moving connection piece). But the reasoning was stronger the other way: **Forculus = the hub = the gate users pass through = the solid thing** and **Limentinus = the agent = watches over each remote threshold = plural, per-node guardian**. Names and roles lined up better that way.

---

## 14. Cardea rejection details (agent naming)

Cardea was the second candidate for the agent name before Limentinus won.

### Candidate: Cardea

**Meaning:** Roman goddess of door hinges. Ovid: *"Her power is to open what is shut, to shut what is open."* Semantic fit: the agent is the moving connecting piece between central and remote.

**GitHub conflict check — mixed:**

| Repo | Stars | Language | What it is |
|------|-------|----------|------------|
| `MLBazaar/Cardea` | 122 | Python | *"An open source automl library for using machine learning in healthcare"* — different domain, but most prominent by stars |
| `vickytilotia/Django_CarDealer_App` | 19 | Python | Car dealer app (happens to include "Cardea" in name) |
| `hyperledger-labs/cardea` | 8 | — | Hyperledger healthcare agent |
| `LX-schlee/Web_Scraping_BeautifulSoup_CarDealer` | 6 | Jupyter | Unrelated car-dealer scraper |
| `maisn3r/QB-Cardealer` | 2 | — | QBus car dealership script (FiveM gaming) |
| `Dev-Daljeet/CarDealershipSystem` | 5 | Java | Spring Boot car dealer |
| `cardea-mcp/cardea-cli` | 13 | Rust | *"OpenMCP Server Proxy CLI"* |
| `hyperledger-labs/cardea-mobile-agent` | 9 | JS | Hyperledger health agent |
| `hectorm/cardea` | 15 | Go | *"Cardea is an **SSH bastion server** with access control, session recording, and optional TPM-backed key protection."* — **closest domain conflict** |
| `hyperledger-labs/cardea-health-issuer-ui` | 2 | JS | Hyperledger health UI |

**Concerning:** `hectorm/cardea` is an infrastructure agent in adjacent security space. `hyperledger-labs` cluster owns the "Cardea" brand in healthcare identity. Both are active enough to create namespace noise.

### Limentinus accepted instead

**Meaning:** Roman god-spirit of thresholds — named directly after *limen* itself. The protective guardian watching over each doorway.

**GitHub check:**

| Repo | Stars | Language | What it is |
|------|-------|----------|------------|
| `e6tUcu7c9h/limentinus` | 0 | — | (empty placeholder) |

One repo. Zero stars. Effectively pristine.

**Decision rationale:** Limentinus is recursive with "Limen" (good — ties the family name directly into the component), zero GitHub conflicts, and the mythological meaning (guardian of the threshold) captures the agent's role (watching over each remote host's "threshold" to Limen) beautifully.

---

## 15. Clean architecture rule refinement (mid-brainstorm correction)

### How the correction happened

Mid-section-2 of design presentation, after showing Limen's proposed folder layout (mirroring the default clean-architecture pattern where Domain holds value objects, Application holds DTOs/services/commands/queries etc.):

> *"stoppp! for the clean architecture, on the domain are ONLY db models NOTHING else on the Infrastructure are db migration everythingi db ish and any external integration. every service and command and query, as well as dtos and models go ino applicartion also make the folder Queries and Commands. for the command and queries, make one file containing the query/queryhandler or command/command handlre please"*

### The strict rules captured

Saved to persistent memory (`~/.claude/projects/C--GithubProjects/memory/feedback_clean_architecture_layout.md`) and documented in:
- Design spec §5
- HANDOFF §2.11
- Every repo's CLAUDE.md

**Rules:**

- **Domain layer:** ONLY database entity models. Nothing else. No value objects, no enums, no services, no interfaces, no abstractions. Just classes that map to DB tables.
- **Infrastructure layer:** ALL DB code (EF, DbContext, migrations, repositories). Plus ALL external integrations (gRPC/WS/HTTP clients, OIDC, Quartz, Docker API, subprocesses, Kestrel/YARP wiring, ACME).
- **Application layer:** EVERYTHING else (services, commands, queries, DTOs, validators, interfaces, Mediator behaviors).

- **Sub-structure of Application:** Top-level `Commands/` and `Queries/` folders (capitalized), organized by feature/domain (`Commands/Nodes/`, `Queries/Services/`).

- **One file per command or query:** the file contains BOTH the command/query type AND its handler. Example: `CreateServiceCommand.cs` contains `CreateServiceCommand` record + `CreateServiceCommandHandler` class in the same file. **Do NOT split them.**

### Why this matters

These aren't style preferences — they're enforced conventions the user has derived from previous C# projects. Breaking them in any Limen repo is a bug. The rules propagate to every component's CLAUDE.md.

### Why "one file per command/query"

The user's reasoning: a command and its handler are coupled — the handler exists solely to serve that command. Splitting them into two files adds navigation cost without benefit. One file = one complete CQRS unit.

---

## 16. Plan decomposition rationale (why 7 plans)

### Scope pressure

The spec covers 4 components + UI + auth + WG + deploy queue = 6+ subsystems. Any single implementation plan would be unreadable and wouldn't allow incremental shipping.

Per the `superpowers:writing-plans` skill: "If the spec covers multiple independent subsystems, suggest breaking this into separate plans — one per subsystem. Each plan should produce working, testable software on its own."

### The 7-plan breakdown

| # | Plan | Ships working software that… |
|---|------|-------------------------------|
| 1 | Foundation | Admin can log in to an empty Limen dashboard via OIDC. Scaffolding, CI, Docker image in GHCR. |
| 2 | Agent control channel | Admin generates provisioning key; Limentinus enrolls; node appears in UI with heartbeat. (No WG, no Docker yet.) |
| 3 | WireGuard tunnels | All Limen↔Limentinus traffic now flows over WG. Forculus hub operational. |
| 4 | Ostiarius + TLS | Admin adds service + route; Ostiarius serves with auto-LE. No auth gate yet. |
| 5 | Docker deploys + auto-update | Services deploy on docker-role nodes; registry polling triggers redeploy; rollback on health fail. |
| 6 | Resource auth | password/sso/allowlist modes; Ed25519 JWT; Ostiarius AuthMiddleware; magic-link flow. |
| 7 | Polish | Full E2E suite; per-repo READMEs; docs site; release pipeline; v0.1.0 tagged. |

Each plan ends with working, demoable software. You could stop at any plan and have something real.

### Why this order

- **1 before 2:** Need the manager before agents can enroll into it
- **2 before 3:** Need the control channel to enroll agents before wrapping it in WG
- **3 before 4:** Need the tunnel to carry traffic before the proxy can route it
- **4 before 5:** Need a place to expose services (proxy) before deploying them makes sense for admin
- **5 before 6:** Need services running before resource-level auth around them is testable end-to-end
- **7 last:** E2E tests can't be written until all components exist to test

### Why each plan has full TDD detail (especially Plan 1)

Per the skill: every step must contain actual content (test code, implementation code, commands, expected output). No "TBD" or "implement later" allowed. Plan 1 was written to full TDD depth as the template; Plans 2-7 reference the same pattern but compress the TDD cadence where the pattern is clearly established.

---

## 17. Kubernetes — on the roadmap since the start

### Why K8s is NOT in v0.1.0

- Scope multiplier — adding K8s orchestration triples the surface area (kubeconfig management, CRDs, Helm, namespaces, probes, rollouts)
- Docker-first in v1 is correct for the admin's homelab target
- Multi-admin RBAC (v0.2.0) is a prerequisite for K8s UX being usable in a team setting

### Why K8s IS on the roadmap (v0.3.0)

User reminded: *"please also add into the roadmap kubernetis suport please as this is also somehting i want to support"*.

Expanded roadmap entry now includes:
- New `k8s` role flag alongside `docker`/`proxy`/`control`
- Two modes: embedded k3s per node OR external cluster via kubeconfig
- Coexists with Docker — some nodes run Docker, some k3s, admin picks per node
- Per-component change list (Limen/Limentinus affected; Ostiarius/Forculus unchanged)
- Auto-deploy works identically (registry poll → bump digest → rolling update via K8s API)
- Explicitly OUT of v0.3.0: Helm mgmt, GitOps CRDs, multi-cluster federation, running Limen itself on K8s

See `docs/roadmap.md` for the current version.

---

## 18. Minor decisions collected

- **Pronunciation:** Limen = /ˈliː.mən/ ("LEE-men"). Anglicized to "LYE-men" is acceptable but classical pronunciation preferred in docs.
- **Identity file permissions:** Limentinus persists identity at `/var/lib/limentinus/identity.json` with mode **0600** (Newt uses 0644 — we tighten it on explicit research recommendation)
- **JWT TTL:** 15 minutes (short enough to limit exposure on leaked tokens; long enough to avoid constant refresh)
- **JWT signing algorithm:** Ed25519 (fast, small keys, well-supported in .NET 10 via NSec.Cryptography). Not RSA.
- **Revocation polling interval:** 30 seconds (sub-minute revocation window acceptable for v1)
- **Key rotation:** supported with 24h overlap (old + new both valid) to allow rolling restarts
- **WG tunnel subnet:** 10.42.0.0/24 in v1 (small, easy to reason about; expandable later if needed)
- **WG MTU:** 1280 (Gerbil's default; avoids fragmentation issues over common NAT paths)
- **Commit style:** Conventional Commits (feat/fix/chore/docs/test/refactor/ci) throughout
- **No AI attribution in commits:** Inherited rule from user's other repos (Kotlin-tls-client, KotifyClient, AutexisCase memory). Hard rule.

---

## 19. Files created during this brainstorm session

All under `C:\GithubProjects\getlimen\` locally and pushed to `github.com/getlimen`:

```
getlimen/
├── .github/                           — org profile + artwork
│   ├── profile/README.md              — public landing page
│   ├── profile/assets/                — SVG logos + PNG avatars + generator script
│   ├── README.md                      — meta-repo description
│   └── LICENSE, .gitignore
├── limen/                             — central manager repo
│   ├── docs/
│   │   ├── HANDOFF.md                 — distilled handoff (316 lines)
│   │   ├── decision-log.md            — THIS FILE
│   │   ├── roadmap.md                 — v2+ deferred, K8s detailed (125 lines)
│   │   ├── research/
│   │   │   └── 2026-04-14-reference-project-analysis.md  — consolidated reference research
│   │   └── superpowers/
│   │       ├── specs/
│   │       │   └── 2026-04-14-limen-design.md             — authoritative design (686 lines)
│   │       └── plans/
│   │           ├── 2026-04-14-plan-01-foundation.md       — Plan 1 full TDD (1918 lines)
│   │           ├── 2026-04-14-plan-02-agent-control-channel.md  (1220 lines)
│   │           ├── 2026-04-14-plan-03-wireguard-forculus.md     (538 lines)
│   │           ├── 2026-04-14-plan-04-ostiarius-proxy.md        (331 lines)
│   │           ├── 2026-04-14-plan-05-docker-deploys.md         (280 lines)
│   │           ├── 2026-04-14-plan-06-resource-auth.md          (285 lines)
│   │           └── 2026-04-14-plan-07-polish-e2e.md             (138 lines)
│   ├── CLAUDE.md                      — project conventions
│   ├── README.md
│   ├── LICENSE, .gitignore, .editorconfig
├── ostiarius/                         — each has CLAUDE.md, README, LICENSE, gitignore, editorconfig
├── forculus/
├── limentinus/
├── limen-cli/                         — stub for v1.1+
└── limen-docs/                        — stub for v1.1+
```

Persistent memory file (outside the repos):
```
~/.claude/projects/C--GithubProjects/memory/
├── MEMORY.md
└── feedback_clean_architecture_layout.md  — the strict layer rules
```

---

*End of decision log. Keep updating as new decisions are made.*
