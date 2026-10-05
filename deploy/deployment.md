# Deployment

For a production server see `deploy/devops-handoff.md` (runbook for the DevOps engineer) and `docker-compose.prod.yml`.
This file covers configuration and the local Docker stack.
The hadith data is not in git: restore the database backup first (see "The database is not created with data automatically" below and `deploy/devops-handoff.md` section 3).

The stack is three parts: SQL Server, the ASP.NET Core API, and the Next.js frontend.
All environment-specific values are configuration; nothing is hardcoded.

## Configuration

| Part | Setting | Purpose |
|---|---|---|
| Frontend (build time) | `NEXT_PUBLIC_API_BASE` | Public API URL, e.g. `https://api.example.com/api`. Inlined into the client bundle, so changing it needs a rebuild. Defaults to `http://localhost:5147/api`. |
| API | `ConnectionStrings__DefaultConnection` | SQL Server connection string. |
| API | `Together__ApiKey` | Together AI key (model `zai-org/GLM-5.3-Flash`, override with `Together__Model`) for AI summaries and Ilal explanations. Takes precedence over Gemini. |
| API | `Together__ExtraBody` | JSON object merged into every chat request. Set `{"reasoning_effort":"low"}` for `zai-org/GLM-5.3-Flash` (turns the model's thinking down; AI check 3–4 s instead of 8–27 s). In Docker it comes from `TOGETHER_EXTRA_BODY`. |
| API | `Gemini__ApiKey` | Gemini key for AI summaries and Ilal explanations. Without it those features fail gracefully. |
| API | `VERIFY_REVIEW_TIMEOUT_SECONDS` | Time limit for the model review step of `/verify` (default 8, range 2–60). On timeout the answer comes from text matching alone. See `docs/verify-feature-handoff.md`. |
| API | `Cors__AllowedOrigins__0`, `__1`, ... | Browser origins allowed to call the API (the frontend URL). Defaults to `http://localhost:3000`. |
| API | `DisableHttpsRedirection=true` | Set when TLS is terminated by a proxy/platform (already set in the Docker image). |

Never commit real keys. Use platform secrets or `dotnet user-secrets` locally.

## Docker

```bash
# API (context = repository root)
docker build -f src/SmartHadithTree.Api/Dockerfile -t hadith-api .
# Frontend (context = frontend/)
docker build --build-arg NEXT_PUBLIC_API_BASE=https://api.example.com/api -t hadith-web frontend
```

Single-host stack with SQL Server:

```bash
export MSSQL_SA_PASSWORD='<strong password>'
export TOGETHER_API_KEY='<key>'   # preferred; or GEMINI_API_KEY as the fallback
docker compose up --build
```

The database is not created with data automatically. The API applies EF migrations on start (empty schema);
load the corpus either by restoring the current backup `backups/SmartHadithTree_Shamela_v5_2026-10-05.bak`
(31 books, 274,597 hadiths; git-ignored, mounted read-only at `/backups` in the `db` container; versions and checksums
in `backups/README.md`) or by rebuilding it with the Shamela pipeline (`docs/data_ingestion.md`, `docs/shamela_migration.md`).

The backup was taken from `SmartHadithTree_ShamelaV5`, the database name the local `appsettings.json` uses. The compose
file connects to a database called `SmartHadithTree`, so restore into that name (`RESTORE DATABASE SmartHadithTree ... WITH MOVE`)
or change `Database=` in the compose connection string. Check the logical file names first with `RESTORE FILELISTONLY`.

## Status

Verified on 2026-10-05 with Docker Desktop on Windows: `docker compose build` succeeds for both images, and
the full stack ran after restoring the v5 backup (about 20 s). The API applied its migrations without errors,
`/api/books` and `/api/search?query=...` return data from the restored database, the web container answers on
port 3000, and the CORS header for `http://localhost:3000` is present.

Notes from that run:
- Put `MSSQL_SA_PASSWORD` in a git-ignored `.env` file next to `docker-compose.yml` (compose reads it automatically).
- Start `db` first, restore, then start `api` and `web`: `docker compose up -d db`, restore, `docker compose up -d api web`.
  The restore needs `WITH MOVE` to the Linux paths, e.g.
  `MOVE 'SmartHadithTree_ShamelaV5' TO '/var/opt/mssql/data/SmartHadithTree.mdf', MOVE 'SmartHadithTree_ShamelaV5_log' TO '/var/opt/mssql/data/SmartHadithTree_log.ldf'`.
- The first web build once failed while next/font fetched Google Fonts and passed on retry, so the build needs
  internet access.
- Not yet checked: AI summaries and Ilal explanations (they need a Together or Gemini key), and `/verify` end to end.
