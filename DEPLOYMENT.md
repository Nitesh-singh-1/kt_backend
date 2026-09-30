# Deployment

Two fully isolated environments run side by side on **the same VPS**
(`129.121.132.103`): **prod** and **UAT**. They share nothing at runtime —
separate containers, separate Docker networks, separate Postgres volumes,
separate secrets, separate ports, separate image tags. See
`docker-compose.uat.yml`'s header comment for exactly why each of those is
deliberate, not incidental.

| | Prod | UAT |
|---|---|---|
| Directory on VPS | `/opt/ktransport/` | `/opt/ktransport-uat/` |
| Compose file | `docker-compose.yml` | `docker-compose.uat.yml` |
| Trigger branch | `main` | `develop` |
| Image tag | `:latest` | `:uat` |
| `api` port | `8080` | `8081` |
| `web` port | `80` | `8082` |
| `postgres` port | `127.0.0.1:5432` | `127.0.0.1:5433` |
| Frontend URL | `http://129.121.132.103` | `http://129.121.132.103:8082` |
| API URL | `http://129.121.132.103:8080/api` | `http://129.121.132.103:8081/api` |

**Workflow:** do day-to-day work against `develop` → it auto-deploys to UAT →
test there → when it's good, merge `develop` into `main` → that auto-deploys
to prod. Nobody pushes straight to `main`.

## How a deploy happens

Both repos are separate git repos, each with **two** deploy workflows:

- `.github/workflows/deploy.yml` — triggers on push to **`main`** → builds,
  tags `:latest` + `:<sha>`, SSHes in and runs
  `docker compose pull <service> && docker compose up -d <service>` in
  `/opt/ktransport`.
- `.github/workflows/deploy-uat.yml` — triggers on push to **`develop`** →
  builds, tags `:uat` + `:uat-<sha>`, SSHes in and runs
  `docker compose -f docker-compose.uat.yml pull <service> && ... up -d <service>`
  in `/opt/ktransport-uat`.

Each workflow only ever touches its own service in its own environment — a
UAT deploy can never restart a prod container, and a backend push never
restarts `web`. There is no manual deploy step; pushing to the right branch
is the deploy trigger.

### The DB/migration edge case, and why it's a non-issue

Because UAT and prod are backed by **physically separate Postgres volumes**
(not a shared database with a schema split), UAT's database can be — and
often will be — at a different migration state or have test data prod
doesn't. This is expected, not a bug: EF Core migrations and first-boot
seeding already self-apply on container startup (confirmed live on this
VPS), so each environment's database just reflects whatever code is actually
running there. There is no code path by which a UAT-only migration or a
piece of UAT test data can reach prod's database — they don't share a
network, a volume, or credentials.

## One-time setup (already done for the current VPS)

1. Docker Engine + Compose plugin installed via `get.docker.com`.
2. `/opt/ktransport/docker-compose.yml` — copy of this repo's `docker-compose.yml`.
3. `/opt/ktransport/.env` (mode 600, never committed) — `POSTGRES_PASSWORD`,
   `JWT_SECRET` (random 32+ chars), `CORS_ORIGIN_0`, `FRONTEND_BASE_URL`, etc.
   See `.env.example` in this repo for the full list.
4. A dedicated ed25519 deploy keypair — public half in the VPS's
   `~/.ssh/authorized_keys`, private half in **both** repos' GitHub Actions
   secrets (`SSH_PRIVATE_KEY`), alongside `SSH_HOST` and `SSH_USER`.
5. Both GHCR packages (`kt_backend`, `ktfrontend`) set to **Public** visibility
   (Settings on the package itself) so the VPS can `docker compose pull` without
   needing a registry login.
6. `/opt/ktransport-uat/docker-compose.uat.yml` — copy of this repo's
   `docker-compose.uat.yml`.
7. `/opt/ktransport-uat/.env` (mode 600, never committed) — its own
   `POSTGRES_PASSWORD` and `JWT_SECRET`, generated fresh, deliberately
   different from prod's. See `.env.uat.example` for the full list.

## Setting up a NEW VPS from scratch

```bash
# 1. Generate a deploy key locally, add the PUBLIC half to the new VPS's
#    ~/.ssh/authorized_keys (any method — cloud console, existing password auth, etc).
ssh-keygen -t ed25519 -f kt_deploy_key -N "" -C "github-actions-deploy"

# 2. SSH in with the new key and bootstrap Docker + the compose stack:
ssh -i kt_deploy_key root@<new-ip> 'bash -s' < scripts/vps-bootstrap.sh
# (see this repo's docker-compose.yml + .env.example for what the script needs
#  to write to /opt/ktransport — there is no committed bootstrap script here
#  since it embeds generated secrets; recreate it from .env.example each time.)

# 3. Add SSH_HOST / SSH_USER / SSH_PRIVATE_KEY to both repos' GitHub secrets.
# 4. Push to main on both repos (or re-run the existing deploy.yml runs) to
#    publish the first images, then flip both GHCR packages to Public.
# 5. docker compose pull && docker compose up -d on the VPS.
```

## Rollback

Every image is also tagged with the git SHA that built it. To roll back a
service to a known-good build:

```bash
ssh root@129.121.132.103
cd /opt/ktransport
docker compose pull  # no-op if already cached
API_IMAGE=ghcr.io/nitesh-singh-1/kt_backend:<good-sha> docker compose up -d api
```

(Set the equivalent `WEB_IMAGE=...:<sha>` for the frontend.)

## Database backups

Nightly `pg_dump` runs via cron on the VPS itself (`scripts/backup-db.sh` in
this repo, deployed to `/opt/ktransport/scripts/backup-db.sh`):

- Schedule: `30 20 * * *` UTC = **2:00 AM IST**, chosen to land outside Indian
  business hours (the VPS runs in UTC — see `timedatectl`).
- Dumps via `docker exec kt_postgres pg_dump` (no DB network exposure needed),
  gzips, timestamps, verifies gzip integrity before trusting the file.
- Retention: last **14 days**, older dumps auto-pruned each run.
- Output: `/opt/ktransport/backups/kt_<timestamp>.sql.gz`, cron log at
  `/opt/ktransport/backups/backup.log`.

**This is Layer 1 only — local disk on the same VPS.** It protects against a
bad migration, an accidental in-app deletion, or DB corruption. It does
**not** protect against losing the VPS itself (disk failure, provider issue,
compromise) — the backups die with it. Layer 2 (an offsite copy — object
storage like Backblaze B2/DO Spaces, or rsync to a second host) is a
deliberate near-term follow-up, not yet wired in.

To restore from a dump:
```bash
gunzip -c /opt/ktransport/backups/kt_<timestamp>.sql.gz | \
  docker exec -i kt_postgres psql -U postgres -d kt
```

## Known follow-ups (not blockers for today's deploy)

- **UAT gets no automated backup.** The nightly cron job only dumps
  `kt_postgres` (prod's container name) — `kt_postgres_uat` is never
  touched, deliberately, since UAT data is disposable test data by
  design. If UAT starts holding anything worth keeping, copy the same
  cron pattern with `_uat` suffixes.
- **UAT secrets currently only exist in this session's history**, not
  in a password manager — same caveat as prod's secrets. Rotate both
  once there's a real secrets-management process in place.
- **Offsite backup copy (Layer 2)** — see "Database backups" above.

- **No HTTPS.** `Program.cs` calls `UseHttpsRedirection()` unconditionally, but
  it degrades to a harmless logged warning (no forced redirect) when no HTTPS
  port is configured — plain HTTP works today. Add a domain + reverse-proxy TLS
  (Caddy or nginx+certbot in front of `api`+`web`) as the next infra slice.
- **No zero-downtime deploy.** `docker compose up -d <service>` briefly drops
  that service while the new container starts. Fine for a low-traffic pilot;
  revisit with a second container + health-gated swap if uptime matters later.
- **BuildExpiryGuard** in the frontend is a client-side date lockout originally
  meant for a preview build; it's currently set to expire 2027-12-31 as a safe
  buffer. See the TODO in that file — proper license/subscription enforcement
  belongs server-side (`TenantSubscription.ExpiresAt`), not a hardcoded client date.
