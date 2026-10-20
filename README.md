# Northwind Support Desk

A support ticket management system: raise tickets, search and filter them, assign them to agents,
and move them through their lifecycle.

**.NET 10 Web API** + **React 19 / TypeScript** + **SQL Server**, in one monorepo.

---

## The assignment

You are joining a team that already has this app working. Complete the tasks **in order**.
Full requirements are in **[`ASSIGNMENT_CANDIDATE.md`](ASSIGNMENT_CANDIDATE.md)**.

| # | Task | Done looks like | Effort |
| --- | --- | --- | --- |
| 1 | **Fix a defect, add server-side filtering** | Root cause fixed with a regression test; every list filter applied in the database with a correct count | ~2.5 h |
| 2 | **Ticket escalation & assignment** | Automatic triage on create, escalation with history, SLA status, UI for all of it | ~7 h |
| 3 | **Login and authentication** | Agents sign in; API rejects unauthenticated calls; login UI | ~4 h |
| 4 | **Dockerise the API** *(optional)* | `docker compose up` gives a migrated, seeded API with no baked-in secret | ~1.5 h |

### Guidance

- **Time: 2 days.** Honestly marking a task "not finished" beats rushing all of them.
- **Fork** this repo to your own GitHub account and work there. **Do not push to the original.**
- **AI tools are allowed.** You must understand and be able to defend every line you submit.
- **Commit as you go** and keep the real history. Commit the Task 1 defect fix and the filtering
  separately from each other and from the feature work. A single squashed commit is not accepted.
- **Where the spec is ambiguous**, make a decision, write it down in the README, and move on.
- **Out of scope:** registration, password reset, roles, OAuth/SSO, refresh tokens, MFA,
  notifications, CI, caching, visual redesign. Mention them under *Known limitations* instead.

### Submission

1. Update this **README** with what you built, your design decisions (chosen, rejected and why),
   the defect's root cause, dev login credentials, how to run and test it, AI usage, and known
   limitations. A reviewer must be able to run it without asking you anything.
2. Add an **APPROACH.md** explaining your approach and thought process for each task.
3. Commit both, then send us the **link to your fork**, the **dev login credentials**, and a
   **rough note of time spent** on what. (If the fork is private, add the reviewers as collaborators.)

---

## Quick start

**Prerequisites:** .NET SDK 10 (`global.json`), Node.js 22 (`.nvmrc`; 20.19+ works), Docker.

```bash
cp .env.example .env          # optional, every value has a working default
npm install                   # installs the web app
npm run db:up                 # SQL Server 2022 in Docker on localhost:1433
npm run api                   # terminal 1: migrates, seeds, then serves the API
npm run web                   # terminal 2: React dev server
```

| What | Where |
| --- | --- |
| Web app | http://localhost:5173 |
| API | https://localhost:7043 (also http://localhost:5043) |
| Swagger | https://localhost:7043/swagger |

In Development the API applies migrations and seeds demo data on startup. **Never commit real
credentials.**

**Visual Studio 2026:** run `npm run db:up`, open `SupportDesk.sln`, set
**SupportDesk.Presentation** as the startup project, and press F5 on the **https** profile.

## Tests

```bash
npm test                      # dotnet test, then the web tests (Vitest)
```

Both pass on a clean checkout and neither needs a database.

## Commands

| Command | What it does |
| --- | --- |
| `npm run db:up` / `db:down` / `db:reset` | Start / stop / wipe SQL Server |
| `npm run api` / `npm run web` | Run the API / the React app |
| `npm run build` | Build both |
| `npm run lint` / `typecheck` / `format` | Web lint and types / `dotnet format` |
| `npm run ef -- migrations add <Name>` | Add an EF Core migration |

## API at a glance

| Endpoint | Description |
| --- | --- |
| `POST /api/auth/login` | Sign in with email and password. Returns a JWT. Open without a token. |
| `GET /api/auth/me` | The signed-in agent, or 401. |
| `GET /api/tickets` | Paged list with `search`, `status`, `priority`, `categoryId`, `customerId`, `assignedAgentId`, `unassignedOnly`, `slaStatus`, `sortBy`, `sortDirection`. Requires a token. |
| `GET /api/tickets/{id}` | One ticket |
| `POST /api/tickets` | Create a ticket |
| `PATCH /api/tickets/{id}/status` · `/assignment` | Change status / assignee |
| `POST /api/tickets/{id}/escalate` | Escalate one priority level. Actor is taken from the JWT, not the body. |
| `GET /api/customers` · `/api/agents` · `/api/categories` | Reference data |

Every endpoint except `POST /api/auth/login` (and Swagger in Development) requires `Authorization: Bearer <token>`.

### Development login (Task 3)

Password for every **active** seeded agent: **`Local_Dev_Password1!`** (a local fixture, hashed at seed time).

| Agent | Email | Can sign in |
| --- | --- | --- |
| Alex Turner | alex.turner@northwind-support.example | Yes |
| Ben Osei | ben.osei@northwind-support.example | Yes |
| Priya Nair | priya.nair@northwind-support.example | Yes |
| Sara Lindqvist | sara.lindqvist@northwind-support.example | Yes |
| Marcus Doyle | marcus.doyle@northwind-support.example | Yes |
| Yuki Tanaka | yuki.tanaka@northwind-support.example | No (inactive; same 401 as a wrong password) |

Errors are `ProblemDetails`: `400` validation, `404` not found, `409` conflict or business rule.

**Seed data:** 5 categories, 6 customers (3 Premium), 6 agents (one inactive, one at their limit)
and 40 tickets across every status, priority and SLA state.

## Authentication (Task 3)

Agents sign in with email and password. There is no registration, password reset, refresh token or role model.

**Credential storage.** A `PasswordHash` column on `Agents` (migration `Task3_AddAgentCredentials`). Login is agent-only and email is already unique, so a separate Users table would be a 1:1 join with no extra behaviour. Rejected: storing the password in a Users table “for later customer login” — that is out of scope.

**Hashing.** ASP.NET Identity `PasswordHasher<object>` (PBKDF2, salted, iterated). Never plain text. Hashes are produced at seed time, not checked in. Empty hashes left by the migration default on an existing local database are backfilled on startup.

**Tokens.** HMAC-SHA256 JWT. Signing key from `Jwt:SigningKey` / `Jwt__SigningKey` (listed in `.env.example`). Not a literal in code. Issuer `northwind-support`, audience `northwind-support-web`. **Lifetime is 8 hours (480 minutes)** — one support shift — via `Jwt:LifetimeMinutes`.

**Authorization.** A fallback policy requires an authenticated user on every endpoint. `POST /api/auth/login` is `[AllowAnonymous]`. Swagger UI is middleware, so it stays open. `escalatedBy` is the signed-in agent's name from the token.

**Browser storage.** The JWT is kept in `sessionStorage` under `northwind.auth` and attached by `apps/web/src/api/client.ts`. Trade-off: it survives a refresh and is cleared when the tab closes, which matches a desk session, but any XSS on the origin can read it while the tab is open. An httpOnly cookie would resist XSS and introduce CSRF; that is a production change, not this task.

**Production changes that this assignment does not make:** a real secret in a vault or environment, HTTPS-only cookies or a BFF, refresh tokens, lockout, and rotating the signing key.

## Design decisions (Task 3)

- **Column on Agent, not a Users table.** See above. Chosen because the only principals are agents and email is already a unique login identifier.
- **Identity PasswordHasher over BCrypt.** First-party, no extra native dependency, already the assignment's example of a vetted mechanism.
- **Same 401 for wrong email, wrong password, and inactive agents.** The spec requires identical failures for unknown email vs wrong password. Inactive is included so a disabled account cannot be probed.
- **sessionStorage over localStorage or memory.** Refresh should not bounce an agent to login; closing the tab should. Documented as a XSS trade-off.
- **Fallback authorization policy** rather than `[Authorize]` on every controller, so a new endpoint cannot be left open by accident.

## Known limitations (Task 3)

No registration, password reset, email verification, OAuth/SSO, refresh tokens, MFA, lockout, rate limiting, or roles. Tokens last 8 hours with no silent renew — an expired token is a 401 and a trip back to `/login`. The JWT lives in sessionStorage (XSS-readable). HTTPS certificates are the ASP.NET development cert only.

## Testing (Task 3)

```bash
npm test
```

Covered: wrong password and unknown email share the same 401 and issue no token; inactive agents cannot log in; a successful login returns a token and the agent to greet; password verification passes and fails correctly; the web client attaches the Bearer token and treats a 401 as a cleared session plus redirect; unauthenticated visitors are sent to `/login`; the login form does not call the API when empty and shows the server message on rejected credentials.

Not tested here: token lifetime clock, Swagger's Authorize button, browser cookie behaviour.

## Troubleshooting

| Symptom | Fix |
| --- | --- |
| API cannot reach the database | `npm run db:up`, wait ~20 s, check `docker compose ps` |
| Port 1433 already in use | Change the port in `docker-compose.yml` and `.env` |
| Browser warns about the certificate | `dotnet dev-certs https --trust`, or use the web app, which proxies |
| Web app shows "Unable to load tickets" | The API is not running, or `VITE_API_PROXY_TARGET` points at the wrong port |
| SQL Server slow on Apple Silicon | Enable Rosetta in Docker Desktop → Settings → General |
