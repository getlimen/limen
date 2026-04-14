# Limen

> *Latin: "threshold"* — the stone at the bottom of a Roman doorway, marking the boundary between inside and outside.

**Limen** is a self-hosted infrastructure platform. One tool does what normally takes three:

- **Docker deploy management** — auto-deploy services when their container image is updated in a registry
- **Reverse proxy with automatic TLS** — public ingress, hostname routing, Let's Encrypt certificates
- **WireGuard hub-and-spoke VPN** — connect remote sites without opening inbound ports on them

One admin. One Angular UI. PostgreSQL-only. Docker compose to install. Apache 2.0.

> ⚠️ **Status: in active development. v0.1.0 not yet released.** See [`docs/HANDOFF.md`](docs/HANDOFF.md) and the plans under [`docs/superpowers/plans/`](docs/superpowers/plans/).

## Four components

Named after Roman doorway deities — Romans had a specific deity for each part of a door.

| Repo | Role |
|------|------|
| **[limen](https://github.com/getlimen/limen)** *(threshold)* | Central manager — this repo |
| **[ostiarius](https://github.com/getlimen/ostiarius)** *(doorkeeper)* | Reverse proxy + TLS |
| **[forculus](https://github.com/getlimen/forculus)** *(door panel)* | WireGuard hub |
| **[limentinus](https://github.com/getlimen/limentinus)** *(threshold guardian)* | Universal node agent |

## Quick start *(when v0.1.0 ships)*

```bash
git clone https://github.com/getlimen/limen
cd limen
cp template.env .env
# edit .env: set POSTGRES_PASSWORD, OIDC_*
docker compose up -d
```

Then:
1. Open `http://localhost:8080`, sign in via your OIDC provider
2. Click "Add node" → copy the provisioning key + compose snippet
3. On any host, run the snippet to install Limentinus
4. In the UI, add a Service (Docker image) and a Route (public hostname)
5. Point DNS to your proxy node → traffic is live with auto TLS

## Architecture

See [`docs/superpowers/specs/2026-04-14-limen-design.md`](docs/superpowers/specs/2026-04-14-limen-design.md).

Hub-and-spoke:
- Every managed host runs **Limentinus** (the universal agent)
- One host with `control` role runs **Limen** + **Forculus** + Postgres
- Hosts with `proxy` role run **Ostiarius** (public TLS terminator)
- Hosts with `docker` role run the actual services

## Tech stack

.NET 10 / ASP.NET Core • Angular 21 + spartan.ng + Tailwind 4 • PostgreSQL (sole datastore) • Quartz.NET • YARP + LettuceEncrypt-Archon • WireGuard (kernel via `wg` CLI on server, userspace `wireguard-go` on agents) • Docker compose for deploy

## Contributing

Before opening an issue or PR, please read:

- [`CLAUDE.md`](CLAUDE.md) — architecture conventions (strict clean architecture rules)
- [`docs/HANDOFF.md`](docs/HANDOFF.md) — full project context

## License

[Apache 2.0](LICENSE)
