# Backup and Restore

## What to back up

| Component | Data | Location | Re-derivable? |
|-----------|------|----------|---------------|
| Limen | PostgreSQL database | `compose.yml` postgres volume | **No** — primary datastore |
| Limen | Ed25519 signing key | `/data/signing-key.bin` (or `Auth:SigningKeyPath`) | No — losing it invalidates all active sessions |
| Forculus | WireGuard config | `/etc/wireguard/wg0.conf` | Yes — re-derived from Limen DB on boot |
| Ostiarius | TLS certificates | `/data/certs/` | Yes — re-issued via ACME on restart |
| Limentinus | Identity file | `/var/lib/limentinus/identity.json` | No — but agent can re-enroll with a new key |

## Backup procedure

### Database (required)

```bash
# From the host running the Limen control node:
docker compose exec postgres pg_dump -U limen -d limen > limen-backup-$(date +%F).sql
```

Schedule this via cron (daily recommended).

### Signing key (recommended)

```bash
docker compose cp limen:/data/signing-key.bin ./signing-key.bin.bak
```

## Restore procedure

1. Deploy a fresh Limen instance via `docker compose up -d`.
2. Stop Limen temporarily: `docker compose stop limen`.
3. Restore the database:
   ```bash
   docker compose exec -T postgres psql -U limen -d limen < limen-backup-YYYY-MM-DD.sql
   ```
4. Restore the signing key:
   ```bash
   docker compose cp ./signing-key.bin.bak limen:/data/signing-key.bin
   ```
5. Start Limen: `docker compose start limen`.
6. Agents will auto-reconnect. Routes and services will resume.

## Notes

- If the signing key is lost, all active resource-auth sessions become invalid. Users must re-authenticate.
- If an agent's identity file is lost, delete the node in the Limen UI and re-enroll with a new provisioning key.
- Forculus and Ostiarius are stateless (rebuild from Limen on boot) — no backup needed for them.
