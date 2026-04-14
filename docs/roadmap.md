# Limen — Roadmap

## Shipping order

| Version | Planned contents |
|---------|------------------|
| **v0.1.0** | Plans 1-7 complete: control plane, WG hub-spoke, custom reverse proxy, Docker deploys, resource auth (password/sso/allowlist) |
| **v0.2.0** | Multi-admin + RBAC, CLI (`limen-cli`), docs site polished |
| **v0.3.0** | Kubernetes / k3s support on `docker`-role nodes (dual-mode per node) |
| **v0.4.0** | Registry webhooks (in addition to polling), blueprints (Newt-style pre-defined stacks), cert expiry email alerts |
| **v0.5.0** | PIN auth mode, WebAuthn, API token mode, per-path auth rules (`/admin/*` requires SSO) |
| **v1.0.0** | Stabilization, 6 months of community feedback incorporated |
| **v1.1+** | HA control plane (multi-instance Limen with leader election over Postgres) |
| **v2.0** | Enterprise edition (open-core split): SAML SSO, audit logs, air-gapped registry, support SLA. BSL-licensed separately from the Apache-2.0 community edition. |

## Out of scope — will never be built

- Arbitrary container runtimes beyond Docker + K8s (no Podman-specific quirks, no LXC, etc.)
- Hosted SaaS (always self-hosted)
- Image building / buildpacks (Limen deploys pre-built images; Coolify's lane)
- Replacing Traefik/Caddy for users who already run them (Limen's proxy is Limen-native — we don't accept bring-your-own-proxy)
- Mesh (peer-to-peer) VPN topology (hub-and-spoke only)

## v2+ feature ideas (unprioritized)

- **GitOps mode** — point Limen at a git repo with YAML service definitions; Limen reconciles
- **Backup automation** — scheduled `pg_dump` to S3-compatible storage
- **Multi-region control plane federation** — separate Limen instances sync a shared service catalog
- **Node auto-scaling** — spin up VPS provider nodes on demand
- **One-click template catalog** — Coolify-style "deploy Supabase / Plausible / n8n in one click"
- **Stack blueprints** — multi-container applications (service + DB + cache) deployed as a unit

## Deferred architectural decisions

- JWT revocation list pagination strategy (storage growth at scale)
- Whether Limen.Contracts NuGet is the right distribution method or whether git submodules work better
- HA control plane: Postgres LISTEN/NOTIFY vs external coordinator (Raft-based? etcd-inspired leader lease?)
- Multi-region routing (do we replicate state per region, or keep one central and accept latency?)
