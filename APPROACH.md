# Approach

## Task 3 — Login and authentication

The API was open. Task 3 closes it: agents sign in, everything else needs a JWT, and the UI has a login page plus a signed-in session.

**Credentials on `Agent`.** Email was already unique and the only people who log in are agents. A Users table would be a 1:1 join until customer login exists, which it does not. `PasswordHash` is a column, never a DTO field.

**Hashing outside the domain.** The domain stores a hash and does not know PBKDF2. Infrastructure uses Identity `PasswordHasher`. Seed hashes at seed time with `Local_Dev_Password1!`. An existing local database that got an empty default from the migration is backfilled on startup.

**Auth as application use cases.** Login and “who am I” are handlers. Presentation issues the JWT and reads claims. A fallback authorization policy means a new controller cannot ship anonymous by default. Login is the exception (`AllowAnonymous`). Swagger is middleware, so it stays reachable.

**Same 401 for every failed login.** Unknown email, wrong password, and inactive (Yuki) all return `Invalid email or password.` Verify still runs when the agent is missing so the work is similar.

**`escalatedBy` from the token.** The escalate body is only a reason. The signed-in agent's name is written to history.

**Frontend.** `/login` is outside the authenticated shell. `sessionStorage` holds the token (refresh-friendly, tab-scoped). `client.ts` attaches `Authorization` and treats any 401 other than login as “session dead, go to `/login?from=…`”.

## Task 4 — Dockerise the API

The API only ran on a host machine with .NET 10 installed. Task 4 packages it into a production-grade, secure container that starts alongside SQL Server with a single command.

**Multi-stage build & layer caching.** Using `mcr.microsoft.com/dotnet/sdk:10.0` for building and `mcr.microsoft.com/dotnet/aspnet:10.0` for runtime yields an image of ~348 MB (saving ~70% over the SDK). To make developer iteration fast, `global.json`, `Directory.Build.props`, `Directory.Packages.props`, and the 4 `.csproj` files are copied first to run `dotnet restore`. Only then is source copied and published with `--no-restore`. Editing a `.cs` file reuses the cached restore layer (`CACHED`) in milliseconds.

**Least privilege execution.** The runtime container runs under `USER app` (UID 1654), avoiding root execution. `docker compose exec api whoami` verifies `app`.

**No baked-in configuration.** All database connection strings, JWT signing keys, token parameters, and environments are supplied dynamically via runtime environment variables in `docker-compose.yml` and `.env.example`.

**Robust orchestration.** The compose `api` service waits on `sqlserver` with `condition: service_healthy` so it never crashes while SQL Server is initializing. The service is published on `localhost:5080` and exposes an anonymous `GET /health` endpoint (`DatabaseHealthCheck`) verifying database reachability.

**Production migration strategy.** For local development, running `Database.Migrate()` on startup against an empty volume is zero-touch. For real deployments, this is avoided: horizontally-scaled multi-replica deployments would race on migrations, and the application runtime user should not have elevated DDL permissions. Instead, idempotent SQL scripts (`dotnet ef migrations script --idempotent`) should be applied via deployment pipelines or a single-run Kubernetes init Job prior to rolling out API pods.

