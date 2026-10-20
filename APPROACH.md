# Approach

## Task 3 — Login and authentication

The API was open. Task 3 closes it: agents sign in, everything else needs a JWT, and the UI has a login page plus a signed-in session.

**Credentials on `Agent`.** Email was already unique and the only people who log in are agents. A Users table would be a 1:1 join until customer login exists, which it does not. `PasswordHash` is a column, never a DTO field.

**Hashing outside the domain.** The domain stores a hash and does not know PBKDF2. Infrastructure uses Identity `PasswordHasher`. Seed hashes at seed time with `Local_Dev_Password1!`. An existing local database that got an empty default from the migration is backfilled on startup.

**Auth as application use cases.** Login and “who am I” are handlers. Presentation issues the JWT and reads claims. A fallback authorization policy means a new controller cannot ship anonymous by default. Login is the exception (`AllowAnonymous`). Swagger is middleware, so it stays reachable.

**Same 401 for every failed login.** Unknown email, wrong password, and inactive (Yuki) all return `Invalid email or password.` Verify still runs when the agent is missing so the work is similar.

**`escalatedBy` from the token.** The escalate body is only a reason. The signed-in agent's name is written to history.

**Frontend.** `/login` is outside the authenticated shell. `sessionStorage` holds the token (refresh-friendly, tab-scoped). `client.ts` attaches `Authorization` and treats any 401 other than login as “session dead, go to `/login?from=…`”.
