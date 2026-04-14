# CLAUDE.md — limen (central manager)

> **Project Name**: Limen
> **Owner**: Niclas (PianoNic) — pianonic.ch
> **Type**: Self-hosted infrastructure platform — central control plane
> **Role of this repo**: Central manager. Contains the full design spec, plans, HANDOFF, and the Angular + ASP.NET Core manager itself.

---

## Start here

If you're a new agent or contributor, read these in order:

1. **`docs/HANDOFF.md`** — complete context for the project: what was decided, why, what's done, what's left
2. **`docs/superpowers/specs/2026-04-14-limen-design.md`** — authoritative design spec
3. **`docs/superpowers/plans/2026-04-14-plan-0X-*.md`** — pick up the next pending plan

## Project Overview

Limen combines three capabilities in one self-hosted platform:

1. **Docker deploy management** (like Coolify) — auto-deploy services from registry image updates
2. **Reverse proxy with TLS** (like Traefik/Caddy) — public ingress, routing, Let's Encrypt
3. **WireGuard hub-and-spoke VPN** (like Pangolin) — connect remote sites without public inbound ports

Four components, Roman doorway themed:

- **`limen`** *(threshold)* — this repo; central manager (C# + Angular + Postgres)
- **`ostiarius`** *(doorkeeper)* — reverse proxy (C# NativeAOT + YARP + LettuceEncrypt-Archon)
- **`forculus`** *(door panel)* — WireGuard hub (C# NativeAOT + `wg` CLI subprocess)
- **`limentinus`** *(guardian)* — universal node agent (C# NativeAOT + wireguard-go userspace)

## Tech Stack

| Layer | Technology | Notes |
|---|---|---|
| **Backend** | .NET 10 / ASP.NET Core | Minimal APIs |
| **Architecture** | Clean/onion + CQRS + source-generated Mediator | See "Clean architecture rules" below — strict |
| **Frontend** | Angular 21 + spartan.ng + Tailwind 4 | Standalone components, signals, new control flow |
| **ORM** | EF Core | Code-first migrations, PostgreSQL provider |
| **Database** | PostgreSQL | **Sole datastore** — no Redis, no MinIO |
| **Cache** | `IMemoryCache` | In-process only |
| **Pub/sub** | Postgres `LISTEN/NOTIFY` | If ever needed |
| **Background jobs** | Quartz.NET (Apache 2.0) | NOT Hangfire |
| **Auth (admin)** | OIDC (provider-agnostic, default example: Pocket ID) | Single admin for v1 |
| **Auth (resource)** | password/sso/allowlist per route; Ed25519-signed JWT | See spec §10 |
| **Containerization** | Docker compose only | No ISO, no K8s in v1 |

## Clean architecture rules — STRICT

- **Domain layer** — ONLY DB entity models. No value objects, no enums, no services, no interfaces.
- **Infrastructure layer** — ALL DB code AND all external integrations (WS/HTTP clients, OIDC, Quartz, Docker API, subprocesses).
- **Application layer** — EVERYTHING else: services, commands, queries, DTOs, validators, interfaces, Mediator behaviors.

**Sub-structure of Application:**
- Top-level `Commands/` and `Queries/` folders (capitalized)
- Organize by feature: `Commands/Nodes/`, `Queries/Services/`

**One file per command or query.** The file contains BOTH the type AND its handler. Example: `CreateServiceCommand.cs` contains `CreateServiceCommand` record + `CreateServiceCommandHandler` class. Do NOT split them.

## Project Structure

```
limen/
├── .github/workflows/         # CI, release pipelines
├── contracts/
│   └── Limen.Contracts/       # shared DTOs, published as NuGet
├── docs/
│   ├── HANDOFF.md             # start here if new
│   ├── roadmap.md             # v2+ deferred work
│   ├── superpowers/
│   │   ├── specs/             # design spec
│   │   └── plans/             # implementation plans 01-07
│   └── research/              # reference project analyses
├── src/
│   ├── Limen.API/             # ASP.NET Core entry, WS handlers, endpoints
│   ├── Limen.Application/     # Commands/, Queries/, Services/, DTOs/, interfaces
│   ├── Limen.Domain/          # ONLY DB entity models
│   ├── Limen.Frontend/        # Angular 21 app
│   ├── Limen.Infrastructure/  # EF, gRPC clients, external integrations
│   └── Limen.Tests/
├── compose.yml
├── compose.dev.yml
├── Limen.slnx
├── template.env
├── .editorconfig, .gitignore, LICENSE, README.md
```

## Conventions

- **Language**: English for everything
- **Commits**: Conventional commits (feat/fix/chore/docs/test/refactor/ci)
- **NO AI/Claude attribution in commits or PRs.** Inherited from other PianoNic repos.
- **Branches**: work on feature branches, PR into `main`
- **Reviews**: run `superpowers:code-reviewer` agent before merging non-trivial PRs

## Testing

- **Unit**: xUnit + FluentAssertions + NSubstitute
- **Integration**: Testcontainers.PostgreSql for DB-backed tests
- **E2E**: Playwright + `compose.e2e.yml` harness (see Plan 7)
- **Frontend**: Karma + Jasmine for units; Playwright smoke per feature area

## Where things happen

| Concern | Lives in |
|---------|----------|
| HTTP endpoints | `src/Limen.API/Endpoints/` (file per feature) |
| WebSocket handlers | `src/Limen.API/` (`AgentsWebSocketEndpoint.cs`, `ProxiesWebSocketEndpoint.cs`) |
| Auth-related commands | `src/Limen.Application/Commands/Auth/` |
| Node enrollment logic | `src/Limen.Application/Commands/Nodes/EnrollAgentCommand.cs` |
| Route/service mgmt | `src/Limen.Application/Commands/{Routes,Services}/` |
| Deployment pipeline orchestration | `src/Limen.Application/Commands/Deployments/` (Limen side) + `limentinus` repo (execution side) |
| Quartz jobs | `src/Limen.Infrastructure/Jobs/` |
| EF mappings | `src/Limen.Infrastructure/Persistence/Configurations/` |
| Angular features | `src/Limen.Frontend/src/app/features/<feature>/` |

## Full context

See `docs/HANDOFF.md` for the complete conversation-level context about why every decision was made.
