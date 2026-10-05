# DevOps handoff — Smart Hadith Tree

Everything needed to deploy and operate the app on one Linux host. Background and local-run notes are in
`deploy/deployment.md`; this file is the production runbook.

## What you need from the project owner

Not in the repository. Status as agreed with the project owner:

- [x] **Database backup file** `SmartHadithTree_Shamela_v5_2026-10-05.bak` (474 MB): sent privately by the owner. Check its SHA-256 (section 3). Without it the site shows no data.
- [x] **Server:** you own and administer the VPS, so no access has to be handed over.
- [ ] **Domains:** you create the web and API hostnames, with DNS A records pointing at the server.
- [x] **Certificate email** (`ACME_EMAIL`), if the bundled Caddy is used: use one of your own.
- [x] **LLM key:** you already have the Together API key and model name. Set `TOGETHER_API_KEY` and `TOGETHER_MODEL` in `.env`. Without them the site works, minus the AI features.
- Not needed: the `data/` folder (section 3).

The target VPS is **shared with other projects and databases**: read "Shared VPS" in section 2 before deploying.

## 1. What runs

| Service | Image / source | Port (internal) | Notes |
|---|---|---|---|
| `db` | `mcr.microsoft.com/mssql/server:2022-latest` | 1433 | Not published to the host. Data in the `sqldata` volume. |
| `api` | built from `src/SmartHadithTree.Api/Dockerfile` (.NET 9) | 8080 | Applies EF migrations on start. |
| `web` | built from `frontend/Dockerfile` (Next.js, standalone) | 3000 | `NEXT_PUBLIC_API_BASE` is baked in at build time. |
| `caddy` | `caddy:2` | 80, 443 | Optional (profile `caddy`): reverse proxy and automatic HTTPS. Skip it on a host that already has a proxy. |

Files: `docker-compose.prod.yml`, `deploy/Caddyfile`, `.env.production.example`. `docker-compose.yml` is the
local stack, not for production (publishes 1433, no HTTPS).

Two public hostnames are needed: `WEB_DOMAIN` (the site) and `API_DOMAIN` (the API the browser calls). The
frontend calls the API from the user's browser, so the API must be public.

## 2. Prerequisites

- Linux host (Ubuntu 22.04/24.04 assumed), **4 GB RAM or more** (SQL Server is capped at `MSSQL_MEMORY_LIMIT_MB`,
  default 2048), about 15 GB free disk (database about 4 GB restored, plus the backup and images), x86-64 (the SQL Server 2022 image is built for x86-64).
- Docker Engine with the Compose plugin (v2.24 or newer).
- DNS A records for `WEB_DOMAIN` and `API_DOMAIN` pointing at the host. If the bundled Caddy is used, ports 80 and 443 must be free and open.
- Outbound internet: image pulls, Let's Encrypt, Google Fonts during the web build, and the LLM provider if keys are set.

### Shared VPS (other projects and databases already running)

**Port and proxy choices are yours.** The defaults (3100, 5147, bundled Caddy on 80/443) are only suggestions, and you are the only one who can see what the host already uses. Before the first start, check:

```bash
sudo ss -tlnp | grep -E ':(80|443|3100|5147)\b'
```

then set `WEB_PORT`, `API_PORT` and `COMPOSE_PROFILES` in `.env` accordingly. A clash fails loudly at start-up (`port is already allocated`) and does not affect the other projects.

The stack is isolated: its own compose project name (`smart-hadith-tree`), network and volumes, and its own SQL Server
container with **no published port**, so it cannot clash with other databases (including an SQL Server on 1433) and
nothing else can reach it. Do not point it at an existing database server; the app needs its own `SmartHadithTree` database.
What to check on the host:

1. **Ports 80/443.** If another proxy (nginx, Traefik, another Caddy) owns them, set `COMPOSE_PROFILES=` (empty) in `.env`
   so the bundled Caddy does not start, and add the two domains to the existing proxy:
   - `WEB_DOMAIN` -> `http://127.0.0.1:${WEB_PORT}` (default 3100)
   - `API_DOMAIN` -> `http://127.0.0.1:${API_PORT}` (default 5147)
   - Return 404 for `/api/admin` and `/api/admin/*` on the API domain (see section 8, risk 1).
   - Terminate TLS there; the API trusts the proxy (`DisableHttpsRedirection=true` is already set in the image).
   - nginx example for the API domain: `location /api/admin { return 404; }` then `location / { proxy_pass http://127.0.0.1:5147; proxy_set_header Host $host; proxy_set_header X-Forwarded-Proto $scheme; }`
2. **Host ports.** `WEB_PORT` and `API_PORT` are bound to `127.0.0.1` only; change them in `.env` if they are taken.
3. **Memory.** SQL Server is capped by `MSSQL_MEMORY_LIMIT_MB` (default 2048); the API and web need roughly 0.5-1 GB more.
   Check `free -h` and the other projects' usage first; lower the cap only if the full-text search gets slow.
4. **Disk and Docker.** About 15 GB free. Do not run `docker system prune` or `docker compose down -v` on a shared host;
   use project-scoped commands as in section 9.

## 3. The database backup (not in git)

The hadith data is **not** in the repository. It lives in a backup file that is git-ignored, so a fresh clone has an
empty database until this file is restored. **Without it the site starts but shows no data.**

- File: `SmartHadithTree_Shamela_v5_2026-10-05.bak` (474 MB, compressed, with checksum)
- SHA-256: `63413b6727c72133ff0e0dd4aad2678da475ca111636a7e6b956c96e0b308394` (also in `backups/README.md`)
- Contents: 31 hadith books, 274,597 hadiths, 1,217,978 isnad transmissions, 23,502 narrators. No secrets.

What to do:

1. **Get the file from the project owner** (private transfer: `scp`, a private expiring link, or a file-transfer service).
2. **Verify it:** `sha256sum SmartHadithTree_Shamela_v5_2026-10-05.bak` must print the SHA-256 above.
3. **Put it in `./backups/`** next to `docker-compose.prod.yml` (create the folder). The compose file mounts it into the
   `db` container as `/backups`.
4. **Restore it before the api starts** (section 4, step 2). The restore takes about 20 seconds.
5. Afterwards the file can be deleted from the server or kept as a baseline; the data now lives in the `sqldata` volume.

The same database can be rebuilt without the backup (`docs/data_ingestion.md`), but that needs a local Shamela 4
installation, so use the backup.

### The `data/` folder is not needed

`data/` (about 4 GB: extracted Shamela books, rijal files, samples) is git-ignored and is **not** needed on the server.
It is input for the ETL pipeline that built the database. The API and web read only from SQL Server at run time, and
`data/` is excluded from the Docker build context (`.dockerignore`). Do not copy it; the backup already contains the result.

## 4. First deployment

```bash
git clone <repo> && cd Smart-Hadith-Tree
cp .env.production.example .env && chmod 600 .env     # fill in every value
mkdir -p backups                                       # put the .bak file here

# 1. Start only the database
docker compose -f docker-compose.prod.yml up -d db

# 2. Restore BEFORE starting the api (see warning below)
docker compose -f docker-compose.prod.yml exec -T db bash -c '/opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -t 0 -Q "
RESTORE DATABASE SmartHadithTree FROM DISK=N'\''/backups/SmartHadithTree_Shamela_v5_2026-10-05.bak'\''
WITH MOVE N'\''SmartHadithTree_ShamelaV5'\'' TO N'\''/var/opt/mssql/data/SmartHadithTree.mdf'\'',
     MOVE N'\''SmartHadithTree_ShamelaV5_log'\'' TO N'\''/var/opt/mssql/data/SmartHadithTree_log.ldf'\'', STATS=20"'

# 3. Start everything
docker compose -f docker-compose.prod.yml up -d --build
```

The restore takes about 20 seconds. The logical file names (`SmartHadithTree_ShamelaV5`, `..._log`) come from the
backup; confirm with `RESTORE FILELISTONLY` if a different backup is used.

> **Warning:** if the api starts before the restore, it creates an empty `SmartHadithTree` database. Then add `REPLACE`
> to the `RESTORE` options (`WITH REPLACE, MOVE ...`) after stopping the api (`docker compose ... stop api`).

### Smoke test

```bash
curl -s https://$API_DOMAIN/api/books | head -c 200                       # list of 31 book names
curl -s "https://$API_DOMAIN/api/search?query=%D8%A7%D9%84%D8%A3%D8%B9%D9%85%D8%A7%D9%84" | head -c 200
curl -s -o /dev/null -w "%{http_code}\n" https://$API_DOMAIN/api/admin/import-gawami   # must be 404
curl -s -o /dev/null -w "%{http_code}\n" https://$WEB_DOMAIN                          # 200
docker compose -f docker-compose.prod.yml logs api | grep -i "migration failed"        # must be empty
```

## 5. Configuration reference

Set in `.env`; compose maps them to the API's configuration keys.

| Variable | Required | Purpose |
|---|---|---|
| `WEB_DOMAIN`, `API_DOMAIN` | yes | Public hostnames. |
| `ACME_EMAIL`, `COMPOSE_PROFILES` | with Caddy | Let's Encrypt contact; `COMPOSE_PROFILES=caddy` starts the bundled proxy (empty = off). |
| `WEB_PORT`, `API_PORT` | no (3100, 5147) | Localhost-only host ports for the web and API containers. |
| `MSSQL_SA_PASSWORD` | yes | SQL Server `sa` password. |
| `MSSQL_MEMORY_LIMIT_MB` | no (2048) | SQL Server memory cap. |
| `TOGETHER_API_KEY`, `TOGETHER_MODEL` | no | Preferred LLM provider; the model defaults to `zai-org/GLM-5.3-Flash`. |
| `TOGETHER_EXTRA_BODY` | recommended | JSON merged into every chat request. Keep `'{"reasoning_effort":"low"}'` (single quotes) for GLM-5.3-Flash: without it the AI check takes 8–27 s instead of 3–4 s. |
| `GEMINI_API_KEY` | no | Fallback LLM provider. |
| `VERIFY_REVIEW_TIMEOUT_SECONDS` | no (8) | Time limit of the model review in `/verify` (2–60). |

Without an LLM key the site works; the AI narrator summaries, Ilal explanations and the `/verify` model review are off.
Changing `API_DOMAIN` needs a rebuild of `web` (`up -d --build web`). Never commit `.env` or keys.

## 6. Updating (CI/CD)

Every push to `master` (a merged PR) deploys automatically: `.github/workflows/deploy.yml` runs the .NET tests and
the frontend lint, then asks the host over SSH to deploy that commit SHA. Pushes that change only docs/`*.md` do not trigger it.

- **On the host** (`/opt/smart-hadith-tree`): `deploy/ssh-deploy-gate.sh`, installed as `/usr/local/sbin/smart-hadith-deploy`,
  is the forced command of the deploy key in `/root/.ssh/authorized_keys` (`command=...,restrict`). It accepts only a
  commit SHA that is on `origin/master` and not older than the live one, checks it out and runs `deploy/deploy-remote.sh` of that commit.
  It is not updated by deploys: after changing it, copy it to `/usr/local/sbin/` by hand.
- **`deploy/deploy-remote.sh`**: skips the rebuild for docs-only changes; backs up the database to
  `backups/predeploy_*.bak` (newest three kept) when `src/SmartHadithTree.Infrastructure/Data/Migrations/` changed;
  tags the running images `:previous`; builds and restarts `api` and `web`; checks `/api/books`, the web root, the
  public domain, the `/api/admin` 404 and the "migration check failed" log line. On any failure it puts the previous
  images and commit back (a migration that already ran is not undone: restore the pre-deploy backup for that).
- **Secrets** (repository): `DEPLOY_SSH_KEY`, `DEPLOY_HOST`, `DEPLOY_KNOWN_HOSTS` (pinned `ssh-keyscan` output).
- **Roll back**: merge a revert PR (it deploys like any merge). The gate refuses commits older than the live one, so
  re-running an old workflow run does not roll back. **Redeploy** the current commit: Actions -> deploy -> Run workflow.
- **By hand**: `ssh root@<host> 'cd /opt/smart-hadith-tree && git pull && ./deploy/deploy-remote.sh'`.

The host's `.env` sets `COMPOSE_FILE=docker-compose.prod.yml:compose.edge.yml` (the second file is host-only and joins
`api`/`web` to the edge proxy network), so plain `docker compose ...` in `/opt/smart-hadith-tree` targets production.

## 7. Backups and restore

Nothing is scheduled yet. Recommended: a nightly job on the host, kept off-host as well.

```bash
docker compose -f docker-compose.prod.yml exec -T db bash -c '/opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -Q "BACKUP DATABASE SmartHadithTree TO DISK=N'\''/backups/SmartHadithTree_$(date +%F).bak'\'' WITH COMPRESSION, CHECKSUM, INIT"'
```

The `./backups` folder is mounted read-write into the `db` container for this. Restore with the command from section 4
(add `REPLACE`, stop the api first). The data is derived from a rebuildable pipeline (`docs/data_ingestion.md`), but a
rebuild needs a local Shamela 4 installation, so treat the backup as the source of truth in production.

## 8. Known risks to address (found while preparing this)

1. **Unauthenticated admin endpoint.** `POST /api/admin/import-gawami` (`AdminController.cs`) has no auth and a
   hard-coded Windows path. `deploy/Caddyfile` returns 404 for `/api/admin/*`; keep that rule, and ideally remove the controller.
2. **No authentication or rate limiting on the API.** The endpoints that call the LLM (`/api/narrators/{id}/ai-summary`,
   Ilal explanations, `/verify`) cost money per request when a key is set. Stock Caddy has no rate limiter; add one
   (a Caddy rate-limit module or a WAF/CDN in front) before publishing the API URL widely.
3. **API uses the `sa` account.** Recommended: create a dedicated SQL login with rights on `SmartHadithTree` only and use it in the connection string.
4. **Migration failures are swallowed.** `Program.cs` logs "Database migration check failed at startup" and keeps
   running. Watch for it in the logs (command in the smoke test) and alert on it.
5. **No health endpoint.** The compose file has a healthcheck only for `db`. `GET /api/books` is a usable liveness probe for the API.
6. **No log or metrics setup.** Logs go to Docker's default json-file driver, with no rotation configured.
   EF Core logs every SQL command at Information level (`appsettings.json`); set `Logging__LogLevel__Microsoft.EntityFrameworkCore=Warning` to cut the volume.
7. **Frontend build needs internet** (Google Fonts via `next/font`). One local build failed on a transient fetch and passed on retry.
8. **Status of this setup:** the local stack (`docker-compose.yml`) was built and run end to end on 2026-10-05.
   `docker-compose.prod.yml` and the Caddyfile only passed config validation (`docker compose config`, `caddy validate`);
   they have **not been run on a real server**. Expect to adjust on the first deploy and update this file.

## 9. Useful commands

```bash
docker compose -f docker-compose.prod.yml ps
docker compose -f docker-compose.prod.yml logs -f api
docker compose -f docker-compose.prod.yml restart api
docker compose -f docker-compose.prod.yml down            # keeps volumes; add -v to DELETE the database and certificates
```
