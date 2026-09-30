# Deployment

KTransport runs as three Docker containers on a single VPS, orchestrated by
`docker-compose.yml` living at `/opt/ktransport/docker-compose.yml` on the server:

| Service | Image | Port | Source |
|---|---|---|---|
| `api` | `ghcr.io/nitesh-singh-1/kt_backend` | `8080` | this repo |
| `web` | `ghcr.io/nitesh-singh-1/ktfrontend` | `80` | [ktFrontend](https://github.com/Nitesh-singh-1/ktFrontend) repo |
| `postgres` | `postgres:17.1-alpine` | `127.0.0.1:5432` (not public) | official image |

**Current target:** `129.121.132.103` (plain HTTP — no domain/TLS yet).

## How a deploy happens

Both repos are separate git repos, each with its own `.github/workflows/deploy.yml`
that triggers **on every push to `main`**:

1. Build the Docker image for that repo's service.
2. Push it to GHCR as `:latest` and `:<git-sha>`.
3. SSH into the VPS and run `docker compose pull <service> && docker compose up -d <service>`.

Each workflow only ever touches its own service — a backend push never restarts
`web`, and vice versa. There is no manual deploy step; merging to `main` is the
deploy trigger.

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

## Known follow-ups (not blockers for today's deploy)

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
