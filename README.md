# Products Service

A product catalogue microservice — .NET 10 Web API with a React + TypeScript
front end — built as a senior developer coding exercise.

The brief asked for a secured Products API, an architecture diagram placing it in
an event-driven estate, and a front end to consume it. The emphasis was
*production grade, not just get it working*, so this README explains the
reasoning as much as the mechanics: what was chosen, what was rejected, and what
is deliberately missing.

```
┌──────────────┐      ┌─────────────────┐      ┌──────────────────────────┐
│ React SPA    │─────▶│  Products API   │─────▶│  SQLite / SQL Server     │
│ Vite + TS    │ JWT  │  .NET 10 · CQRS │  EF  │  (provider is config)    │
└──────────────┘      └─────────────────┘      └──────────────────────────┘
```

---

## Contents

- [Quick start](#quick-start)
- [Architecture](#architecture)
- [Authentication](#authentication-how-to-exercise-the-secured-endpoints)
- [API reference](#api-reference)
- [Running the tests](#running-the-tests)
- [Repository layout](#repository-layout)
- [Design decisions and trade-offs](#design-decisions-and-trade-offs)
- [Known gaps and what I would do next](#known-gaps-and-what-i-would-do-next)

---

## Quick start

### Option 1 — Docker (one command)

```bash
docker compose up --build
```

| | URL |
|---|---|
| Front end | <http://localhost:3000> |
| API | <http://localhost:8080> |
| Swagger UI | <http://localhost:8080/swagger> |
| Health | <http://localhost:8080/health> |

The API seeds ten demo products across several colours on first run, so the
colour filter has something to show immediately. The SQLite file lives on a named
volume and survives `docker compose down`.

### Option 2 — run it directly

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download) and
[Node 20+](https://nodejs.org).

```bash
# Terminal 1 — API on http://localhost:5099
dotnet run --project src/Products.Api

# Terminal 2 — front end on http://localhost:5173
cd frontend
cp .env.example .env.local     # defaults to http://localhost:5099
npm install
npm run dev
```

Swagger is at <http://localhost:5099/swagger>. The database is created and
migrated on start-up; no manual step.

---

## Architecture

### Inside the service — Clean Architecture

Four projects, with dependencies pointing strictly inwards:

```
Products.Api  ──▶  Products.Infrastructure  ──▶  Products.Application  ──▶  Products.Domain
  HTTP, DI,          EF Core, repositories,        CQRS handlers,             entities,
  middleware         migrations                    validators, DTOs           value objects
```

`Products.Domain.csproj` has **no package references and no project references
at all** — the file is deliberately empty. Nothing in the domain knows about EF
Core, ASP.NET, MediatR or JSON. That is not a convention maintained by review; a
reference appearing in that file is the dependency rule being broken, visibly.

Reads and writes are separated (CQRS): `IProductRepository` loads whole
aggregates for the write side, `IProductReadRepository` projects to DTOs for the
read side. Neither exposes `IQueryable`, so EF Core's query-translation rules
cannot leak upwards into application logic.

Cross-cutting concerns are MediatR pipeline behaviours rather than code repeated
in each handler, which means they cannot be forgotten when a handler is added:

| Behaviour | What it does |
|---|---|
| `ValidationBehaviour` | Runs every registered validator; collects *all* failures into one 400 |
| `LoggingBehaviour` | Structured entry/exit logging per request |
| `PerformanceBehaviour` | Warns when a request exceeds 500 ms |

### In the wider system — event-driven

![Architecture](docs/architecture.png)

Products, Orders and Payments are separate bounded contexts, each owning its own
datastore, communicating through a broker rather than calling each other.

**[→ Full write-up in `docs/architecture.md`](docs/architecture.md)** — covers the
event catalogue, an end-to-end order sequence with its failure path, why events
beat synchronous calls for state changes (and where they do not), the cost in
eventual consistency and idempotency, RabbitMQ vs Kafka, and exactly how this
service's existing domain events would become published integration events.

The seam is already in place and load-bearing: `Product.Create` raises
`ProductCreatedDomainEvent`, dispatched only after the transaction commits, with
the domain carrying no dependency on the dispatch mechanism.

---

## Authentication: how to exercise the secured endpoints

`/health` is anonymous. Everything under `/api/products` requires a bearer token.

> **`/api/auth/token` is a stand-in for a real identity provider.** It exists so
> the secured endpoints can be exercised without standing up Azure AD, Auth0 or
> IdentityServer first. See [Design decisions](#3-authentication-is-a-real-jwt-pipeline-with-a-deliberately-fake-issuer)
> for what production would change.

**Demo credentials:** `demo` / `Password123!`

### Via Swagger

1. Open <http://localhost:8080/swagger>
2. `POST /api/auth/token` → **Try it out** → **Execute** with the credentials above
3. Copy the `accessToken` value from the response
4. Click **Authorize** (top right), paste the token, **Authorize**
5. Every secured endpoint now works

### Via curl

```bash
TOKEN=$(curl -s -X POST http://localhost:8080/api/auth/token \
  -H 'Content-Type: application/json' \
  -d '{"username":"demo","password":"Password123!"}' | jq -r .accessToken)

curl http://localhost:8080/api/products -H "Authorization: Bearer $TOKEN"
curl "http://localhost:8080/api/products?colour=Red" -H "Authorization: Bearer $TOKEN"
```

### Via the front end

The sign-in panel is pre-filled with the demo credentials. It calls the same
token endpoint and stores the result, so the reviewer can see how auth is wired
without a real login flow.

---

## API reference

All responses are JSON. Errors are [RFC 7807](https://datatracker.ietf.org/doc/html/rfc7807)
`ProblemDetails`.

| Method | Route | Auth | Purpose |
|---|---|:---:|---|
| `GET` | `/health` | — | Readiness, including a real database probe |
| `GET` | `/health/live` | — | Liveness, deliberately excluding the database |
| `POST` | `/api/auth/token` | — | Issue a demo bearer token |
| `GET` | `/api/auth/me` | ✓ | Echo the caller's claims |
| `GET` | `/api/products` | ✓ | List, paged and sorted |
| `GET` | `/api/products?colour=Red` | ✓ | **Filter by colour** |
| `GET` | `/api/products/colour/{colour}` | ✓ | **Filter by colour** (route form) |
| `GET` | `/api/products/{id}` | ✓ | Fetch one |
| `POST` | `/api/products` | ✓ | Create — 201 + `Location` |
| `PUT` | `/api/products/{id}` | ✓ | Update |
| `DELETE` | `/api/products/{id}` | ✓ | Delete — 204 |

Query parameters on the list endpoints: `page` (default 1), `pageSize` (default
20, **max 100**), `sortBy` (`name`, `price`, `colour`, `sku`, `createdAt`,
`updatedAt`), `sortDescending`, `colour`.

Both `/api/products` and `/api/v1/products` route to the same actions, so the
unversioned contract from the brief keeps working while clients that want to pin
a version can. Versions can also be selected by the `X-Api-Version` header or an
`api-version` query parameter.

### Status codes

| Code | When |
|---|---|
| `200` / `201` / `204` | Success. Create returns 201 with a `Location` header |
| `400` | Validation failed — body lists **every** failing field at once |
| `401` | Missing, malformed, expired or wrongly-signed token |
| `404` | No such product |
| `409` | SKU already in use |
| `429` | Rate limit exceeded — carries `Retry-After` |

---

## Running the tests

```bash
dotnet test                          # all 187 backend tests
cd frontend && npm test              # all 45 front-end tests
```

With coverage:

```bash
dotnet test --collect:"XPlat Code Coverage"
cd frontend && npm run test:coverage
```

| Suite | Tests | What it covers |
|---|---:|---|
| `Products.Domain.Tests` | 59 | Invariants, value objects, domain events, boundaries |
| `Products.Application.Tests` | 74 | Handlers, validators, pipeline behaviours, paging |
| `Products.Api.IntegrationTests` | 54 | Full HTTP pipeline over real SQL, both sides of auth |
| `frontend` | 45 | Form validation, table, filter, API client, app states |

Backend line coverage is ~78%, front end ~83%. The aim was meaningful coverage
rather than a number: every edge case the brief called out has a named test —
invalid colour, empty name, negative price, empty result set — plus the ones that
actually bite in production, such as differently-cased SKUs colliding as
duplicates and pages not overlapping when the sort key is not unique.

Integration tests run against **SQLite, not EF's InMemory provider**. InMemory is
not a relational database: it ignores unique indexes, column lengths and SQL
translation, so a suite built on it would pass while the duplicate-SKU constraint
and the colour-filter query were both broken.

CI runs all four suites plus a type-check and both Docker builds on every push
and pull request — [`.github/workflows/ci.yml`](.github/workflows/ci.yml).

---

## Repository layout

```
├── src/
│   ├── Products.Domain/          entities, value objects, domain events — zero dependencies
│   ├── Products.Application/     CQRS handlers, validators, DTOs, abstractions
│   ├── Products.Infrastructure/  EF Core, repositories, migrations, event dispatch
│   └── Products.Api/             controllers, middleware, auth, DI composition root
├── tests/
│   ├── Products.Domain.Tests/
│   ├── Products.Application.Tests/
│   └── Products.Api.IntegrationTests/
├── frontend/                     Vite + React + TypeScript + Tailwind
├── docs/
│   ├── architecture.md           event-driven design and trade-offs
│   ├── architecture.mmd/.svg/.png
│   └── sequence.mmd/.svg/.png
├── .github/workflows/ci.yml
└── docker-compose.yml
```

---

## Design decisions and trade-offs

### 1. Clean Architecture, and an empty Domain csproj

For a service this size the layering is more structure than the feature set
strictly needs. It earns its place because the brief is explicitly about a
*microservice in a distributed system*: the value is that the domain and
application layers have no idea whether they are called by HTTP, a message
consumer, or a background job. Adding a broker consumer later means a new entry
point, not a rewrite.

The concrete payoff is that `Products.Domain.csproj` is empty. The dependency
rule is enforced by the compiler, not by discipline.

### 2. MediatR at version 12.5.0, FluentAssertions at 7.x

Both libraries moved to paid commercial licences in recent majors — MediatR at
13, FluentAssertions at 8. Both are pinned here to the last release under their
original OSS licence (Apache 2.0), via Central Package Management.

This is the kind of thing that is cheap to get right now and expensive to
discover during a legal review later. It is also why every version in the
solution lives in one file: `Directory.Packages.props` makes version drift
between projects structurally impossible rather than merely discouraged.

### 3. Authentication is a real JWT pipeline with a deliberately fake issuer

The validation is production-shaped. Issuer, audience, signing key and lifetime
are all validated, clock skew is cut from the default five minutes to thirty
seconds, and the signing algorithm is **pinned** — leaving it open is the root of
the classic `alg` confusion attacks.

What is fake is the *issuer*. Production would use an external IdP with
asymmetric signing (RS256) and keys published via JWKS, so this API would hold
only a verification key and never one capable of minting tokens. The token
endpoint would not exist.

The development signing key is committed in `appsettings.Development.json`. That
is deliberate, so the repository clones and runs, and it is safe only because it
is loaded by the Development environment alone. There is no production fallback:
options are validated at start-up, so a deployment without `Jwt__SigningKey`
fails to boot rather than running with a guessable key.

### 4. SQLite by default, SQL Server by configuration

The provider is chosen by configuration, not compiled in, so moving to SQL Server
is a connection string and one setting. SQLite is the default because it makes
`docker compose up` a single command with no external dependency.

**The honest caveat:** the committed migration set targets SQLite. EF Core
migrations are provider-specific, so SQL Server needs its own:

```bash
dotnet ef migrations add InitialCreate \
  --project src/Products.Infrastructure \
  --output-dir Persistence/Migrations/SqlServer
```

Shipping a single migration set and calling the service "database agnostic" would
have been the easier claim and a false one.

### 5. Timestamps stored as UTC `DateTime`, not `DateTimeOffset`

Found by running the service rather than by reading it: SQLite refuses `ORDER BY`
on a `DateTimeOffset` column, and `createdAt` is the default sort — so the most
common query in the service returned a 500.

The fix is a value converter storing UTC `DateTime`. Nothing is lost, because
every timestamp originates from `TimeProvider.GetUtcNow()`. It is applied to
**both** providers deliberately: a conversion present in development but not in
production means ordering behaves differently in the two places, which is exactly
the class of bug that survives a green test suite.

### 6. Rate limits on writes and auth, not on reads

Reads are cheap and idempotent; writes cost a database round trip, and the token
endpoint is what credential stuffing targets. Throttling reads at the same rate
would degrade the front end for no benefit.

Buckets are partitioned per authenticated user, falling back to remote IP. That
fallback is only correct if the address is the *client's* — behind a load
balancer it is the proxy's, which would put every anonymous user on the planet in
one bucket. Hence `UseForwardedHeaders`, with `KnownProxies` left empty and
commented, because clearing it without naming the real proxy lets a client spoof
`X-Forwarded-For` to escape its own limit.

### 7. Controllers rather than minimal APIs

Minimal APIs would be a defensible choice. Controllers were picked because
attribute routing, API versioning, `[Authorize]`, per-endpoint rate limiting and
Swagger's XML documentation all compose more cleanly through attributes, and
because the alternative tends to push cross-cutting concerns into an endpoint
registration file that grows without structure.

### 8. Hand-written mapping, no AutoMapper

`ProductDto.FromEntity` is a handful of assignments that the compiler checks and
that appear in "find usages". A mapping library would add a dependency and move
those errors from build time to run time, in exchange for saving about fifteen
lines.

### 9. Value objects for SKU and Money

A bare `string Sku` lets `"abc-1"` and `"ABC-1"` become two rows behind a unique
index. A bare `decimal Price` puts the "must not be negative" rule wherever
someone remembers to put it. Wrapping both makes the invalid states
unrepresentable and gives each rule exactly one home — and `Money` binds an
amount to its currency, so adding GBP to USD stops being expressible.

### 10. `localStorage` for the demo token — a knowingly wrong choice

A production SPA should not keep a bearer token in `localStorage`: anything
running on the page can read it, so any XSS becomes credential theft. The usual
answer is a short-lived token in memory alongside a refresh token in an
`HttpOnly` cookie.

It is used here because the demo has no refresh flow and a page reload losing the
session would obstruct review. Flagged in the code as well as here, because an
undocumented shortcut is indistinguishable from a mistake.

---

## Known gaps and what I would do next

Stated plainly, because knowing what is missing matters more than pretending
nothing is.

**Not built**
- **No real broker integration.** Domain events are raised and dispatched
  in-process; the outbox and relay that would publish them are described in
  `docs/architecture.md` but not implemented. The extension point is a single
  event handler.
- **No SQL Server migration set** — see decision 4.
- **No authorisation, only authentication.** Every valid token can do everything.
  A role claim is issued and unused, so `[Authorize(Roles = …)]` is a small step,
  but scopes and resource-level permissions are absent.
- **No refresh tokens or revocation.** A leaked token is valid until it expires;
  the short lifetime *is* the mitigation.

**Would come next, roughly in order**
1. **Outbox + relay**, publishing `ProductCreated` and `ProductPriceChanged` to
   RabbitMQ. The single highest-value addition, and the one the architecture is
   already shaped around.
2. **OpenTelemetry tracing**, propagated through message headers so a trace
   survives the hop through the broker. The service already stamps a `traceId` on
   every response and log line; this extends it across services.
3. **Optimistic concurrency** on update. Two concurrent `PUT`s currently last-write-wins
   silently. A `rowversion` and a `409` would make the conflict visible.
4. **Kubernetes manifests** using the liveness and readiness probes that already
   exist, with migrations as a pre-deploy job rather than at start-up — so N
   replicas do not race to migrate one schema.
5. **Cursor-based pagination** alongside offset. `OFFSET` degrades on large
   tables and can skip rows when data shifts between requests; the ordering is
   already given a unique tiebreaker to make a cursor straightforward.
6. **A real IdP**, retiring `/api/auth/token` entirely.

**Deliberately not done**
- No caching layer. Premature without a measured read pattern, and cache
  invalidation on a catalogue that publishes change events deserves a design, not
  a `MemoryCache` sprinkled in.
- No repository generic base class. Two repositories do not justify an
  abstraction that would have to be un-abstracted the first time one of them
  needs something specific.
- No CSP header on the front end. A policy written without knowing the real asset
  origins is either ineffective or breaks the page; better omitted than guessed.

---

## Licence

[MIT](LICENSE)
