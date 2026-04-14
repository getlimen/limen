# Reference project analysis — 2026-04-14

Six reference projects were studied during design via parallel Explore-agent investigations. This document consolidates the findings.

## TL;DR — patterns adopted

| Pattern | Source | Adopted because |
|---------|--------|-----------------|
| Two-tier credentials (provisioning key → permanent ID+Secret) | Newt, Portainer EdgeKey | Frictionless enrollment, rotatable, no pre-shared admin keys |
| Stateless agents — only credentials persisted locally | Newt, Komodo Periphery | Config always fresh from control plane; survives restarts automatically |
| Persistent deployment queue with status/logs/rollback | Coolify `ApplicationDeploymentQueue` | Dedup, history, rollback UX |
| Health check + auto-rollback | Coolify | Prevents cascade failures |
| Domain/feature-based folder organization | Pangolin, Coolify, Komodo | Matches our Commands/Queries rule |
| Config version / chain ID for dedup | Newt | Idempotent reconnects |
| Polling for registry image updates | Komodo (daily) | Fills ecosystem gap — Coolify only does Git webhooks |
| Exponential-backoff WS reconnect | Newt (3s base), Komodo (10s heartbeat) | Standard resilience |
| JSON over WebSocket for agent comms | Pangolin/Newt, Portainer (prefers polling) | Easier to debug than binary proto, AOT-safe, browser dev tools |
| HTTP REST for "dumb relay" (Forculus) | Gerbil | Simple, no bidi stream needed |
| Ed25519 JWT + local verification | Limen original | Avoid per-request round-trip to control plane (Pangolin's badger antipattern) |

## Per-project findings

### Coolify (PHP/Laravel)

**Monolith, domain-organized actions.** SSH-based to remote servers (no agent binary — they deploy a "Sentinel" monitoring container). Persistent deployment queue. Traefik via Docker labels. PostgreSQL source of truth. Webhook-driven Git deploys; auto-deploy on registry image NOT supported (gap we fill).

**Copied:** deployment queue shape, health-check + rollback discipline, real-time WebSocket broadcasts.
**Avoided:** `ApplicationDeploymentJob` 1000+ LoC god class (we decompose into explicit stages); schemaless settings JSON columns.

### Komodo (Rust)

**Core + Periphery split.** WebSocket (custom binary framing) + Noise protocol mutual auth. MongoDB source of truth. Stateless Periphery. Dual connection modes (outbound or inbound). 10-second heartbeat.

**Copied:** Core/Periphery architecture, dual connection modes, stateless agent pattern.
**Avoided:** binary protocol complexity (Komodo's own code calls out "consider JSON over WebSocket"), onboarding keys that never expire, lack of observability.

### Pangolin (TypeScript/Node)

**Three-component hub-and-spoke.** Controls Gerbil (WG) and Newt (agents). Multi-tenant with orgs, sites, resources, targets, RBAC. Traefik delegation via file-config provider. Resource-level auth via badger plugin (calls back to Pangolin every request).

**Copied:** overall hub-and-spoke shape, provisioning-key enrollment, WebSocket for Newt, resource auth concept (the killer feature).
**Avoided:** dynamic build-variant imports (complexity), badger plugin's per-request callback (latency — we do local Ed25519 verify), multi-org modeling (v1 is single-admin).

### Gerbil (Go, Pangolin's WG hub)

**`wgctrl-go` + kernel WG module via netlink.** HTTP-only — peer add/delete via POST/DELETE. Gerbil pulls full config on boot + accepts pushes after. Mutex-protected `wgClient.ConfigureDevice()` calls.

**Copied:** HTTP push + pull-on-boot hybrid, full-replacement boot + incremental runtime, mutex-protected device ops.
**Avoided:** no reconnect-after-Pangolin-restart logic (we add 60s reconcile loop).

### Newt (Go, Pangolin's agent)

**Two-tier credentials.** Provisioning key → POST `/api/v1/auth/newt/register` → get `newtId`+`secret`. WebSocket upgrade with token in query param. Config persisted at `~/.config/newt-client/config.json` (mode 0644 — we use 0600). WireGuard via userspace `wireguard-go` + netstack. Chain-ID dedup on commands. 3s reconnect backoff.

**Copied wholesale:** two-tier credentials, persistent WS with auto-reconnect, userspace wireguard-go, chain-ID dedup.
**Avoided:** 0644 config perms, blocking handlers (we async-dispatch).

### Portainer (Go)

**Two agent modes:** standard (server dials agent) and edge (agent dials home via Chisel tunnel OR pure polling). EdgeKey = base64 composite token. ECDSA signed HTTP requests. Heartbeat via poll timestamps. Endpoint model for multi-environment. Internal proxy factory for API forwarding through Chisel tunnels.

**Copied:** EdgeKey composite token pattern, pull-model when dialing-in not possible, heartbeat via periodic touch.
**Avoided:** WebSocket only for streaming (they use polling for control) — Limen scale is smaller so WS for all control traffic is fine.

## Open questions left after research

1. Whether to use gRPC bidi stream instead of JSON/WS — **decided JSON/WS for debug-ability and ecosystem consensus**
2. Kernel vs userspace WG — **decided kernel via `wg` CLI for Forculus (server) + userspace wireguard-go for Limentinus (cross-platform agents)**
3. Whether to embed reverse proxy in control plane (YARP in-process) — **decided separate Ostiarius container for failure isolation**

Detailed agent reports archived as git history. Clones under `C:\GithubProjects\_research\` (not committed).
