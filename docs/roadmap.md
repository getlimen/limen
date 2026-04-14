# Limen — Roadmap

## Shipping order

| Version | Planned contents |
|---------|------------------|
| **v0.1.0** | Plans 1-7 complete: control plane, WG hub-spoke, custom reverse proxy, Docker deploys, resource auth (password/sso/allowlist) |
| **v0.2.0** | Multi-admin + RBAC, CLI (`limen-cli`), docs site polished |
| **v0.3.0** | **Kubernetes / k3s support** — a new `k8s` role flag alongside `docker`, enabling Limen to deploy services to kubelets on the same node or a connected cluster |
| **v0.4.0** | Registry webhooks (in addition to polling), blueprints (Newt-style pre-defined stacks), cert expiry email alerts |
| **v0.5.0** | PIN auth mode, WebAuthn, API token mode, per-path auth rules (`/admin/*` requires SSO) |
| **v1.0.0** | Stabilization, 6 months of community feedback incorporated |
| **v1.1+** | HA control plane (multi-instance Limen with leader election over Postgres) |
| **v2.0** | Enterprise edition (open-core split): SAML SSO, audit logs, air-gapped registry, support SLA. BSL-licensed separately from the Apache-2.0 community edition. |

## Kubernetes support (v0.3.0) — design notes

K8s is a committed roadmap item, not a "maybe later". The shape it will take:

### New node role: `k8s`

Added alongside the existing `docker` / `proxy` / `control` flags. A `k8s`-role node means Limentinus also knows how to talk to a kubelet — either:

- **Embedded k3s** (Limentinus brings up a lightweight k3s inside the node), OR
- **External cluster** (Limentinus is configured with a kubeconfig pointing at an existing cluster that it manages on the admin's behalf)

Both modes coexist in one Limen install: some nodes run Docker, some run k3s, the admin picks per node.

### What changes in each component

| Component | Change |
|-----------|--------|
| **Limen** | Service model gains `runtime: docker \| k8s` field; new Commands/Queries under `Commands/K8s/` for deployment specs, namespaces, ingresses (Limen-managed) |
| **Limentinus** | New Infrastructure module `K8s/` using `KubernetesClient` (official .NET SDK). DeployPipeline gets parallel stages for K8s — `ApplyManifestStage`, `WaitForRolloutStage`, `RollbackDeploymentStage`. |
| **Ostiarius** | Unchanged — still terminates TLS and routes through WG to backend; backend is just now a K8s service IP instead of a Docker container IP |
| **Forculus** | Unchanged — WG is orthogonal to what runtime actually serves traffic |
| **Contracts** | Extend `DeployCommand` with optional K8s fields (manifest YAML, namespace, kind) |

### Service definition (forward-looking)

The UI will gain a "runtime" picker when creating a Service:

- **Docker service** — image + env + volumes + ports (as today in v0.1.0)
- **K8s service** — YAML manifest (Deployment, StatefulSet, Job) OR a generated manifest from simpler UI fields (image + replicas + probes + resources)

Auto-deploy on image updates works identically — Limen polls the registry, bumps the digest in the manifest, triggers a rolling update via K8s API.

### What's explicitly NOT coming in v0.3.0

- Helm chart management (deferred to later)
- CRD-based GitOps (ArgoCD/Flux replacement — out of scope, users who want GitOps can pair Limen with existing tools)
- Multi-cluster federation (v0.3.0 supports multiple isolated clusters, each owned by one node; federation later)
- Running Limen itself on K8s (still `docker compose` only for Limen's control plane — K8s is for the workloads Limen manages, not Limen's runtime)

### Why v0.3.0 and not v0.2.0

v0.2.0 brings multi-admin RBAC + CLI, which are prerequisites for K8s UX being usable in a team setting (permissions on namespaces, kubectl-style command parity). Doing K8s before multi-admin would need rework once multi-admin lands.

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
