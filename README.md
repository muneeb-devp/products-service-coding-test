# Products Service

A product catalogue microservice: .NET 10 Web API with a React + TypeScript front
end, built as a coding exercise.

The brief asked for a secured Products API, an architecture diagram placing it in
an event-driven system, and a front end to consume it. The emphasis was on
production-grade code, so this README also records the decisions behind it and
what is knowingly left out.

```
┌──────────────┐      ┌─────────────────┐      ┌──────────────────────────┐
│ React SPA    │─────▶│  Products API   │─────▶│  SQLite / SQL Server     │
│ Vite + TS    │ JWT  │  .NET 10 · CQRS │  EF  │  (provider is config)    │
└──────────────┘      └─────────────────┘      └──────────────────────────┘
```

## Contents

- [Quick start](#quick-start)
- [Architecture](#architecture)
- [Authentication](#authentication)
- [API reference](#api-reference)
- [Tests](#tests)
- [Repository layout](#repository-layout)
- [Design decisions](#design-decisions)
- [Known gaps](#known-gaps)

## Quick start

### Docker

```bash
docker compose up --build
```

| | URL |
|---|---|
| Front end | <http://localhost:3000> |
| API | <http://localhost:8080> |
| Swagger UI | <http://localhost:8080/swagger> |
| Health | <http://localhost:8080/health> |

Ten demo products are seeded on first run across several colours, so the colour
filter has something to show. The SQLite file sits on a named volume and survives
`docker compose down`.

### Running it directly

Needs the [.NET 10 SDK](https://dotnet.microsoft.com/download) and
[Node 20.19+ or 22.12+](https://nodejs.org) (Vite 8's requirement; CI uses 24).

```bash
# Terminal 1: API on http://localhost:5099
dotnet run --project src/Products.Api

# Terminal 2: front end on http://localhost:5173
cd frontend
cp .env.example .env.local
npm install
npm run dev
```

Swagger is at <http://localhost:5099/swagger>. The database is created and
migrated at start-up.

## Architecture

### Inside the service

Four projects, dependencies pointing inwards:

```
Products.Api  ──▶  Products.Infrastructure  ──▶  Products.Application  ──▶  Products.Domain
  HTTP, DI,          EF Core, repositories,        CQRS handlers,             entities,
  middleware         migrations                    validators, DTOs           value objects
```

`Products.Domain.csproj` has no package or project references at all; the file is
empty. Nothing in the domain knows about EF Core, ASP.NET, MediatR or JSON, and a
reference appearing in that file means the dependency rule has been broken.

Reads and writes are separated. `IProductRepository` loads aggregates for the
write side, `IProductReadRepository` projects to DTOs for reads. Neither exposes
`IQueryable`, so EF Core's query-translation rules stay in the infrastructure
layer.

Cross-cutting concerns are MediatR pipeline behaviours rather than code repeated
per handler:

| Behaviour | Purpose |
|---|---|
| `ValidationBehaviour` | Runs registered validators, collects all failures into one 400 |
| `LoggingBehaviour` | Structured entry/exit logging |
| `PerformanceBehaviour` | Warns on requests over 500 ms |

### In a wider system

![Architecture](docs/architecture.png)

Products, Orders and Payments are separate bounded contexts, each owning its
datastore, communicating through a broker instead of calling each other.

[Full write-up in `docs/architecture.md`](docs/architecture.md): event catalogue,
an end-to-end order sequence with its failure path, the trade-offs of events
versus synchronous calls, RabbitMQ versus Kafka, and how this service's existing
domain events would become published integration events.

That seam already exists: `Product.Create` raises `ProductCreatedDomainEvent`,
dispatched after the transaction commits, with the domain carrying no dependency
on the dispatch mechanism.

## Authentication

`/health` is anonymous. Everything under `/api/products` needs a bearer token.

`/api/auth/token` stands in for a real identity provider so the secured endpoints
can be exercised without setting up Azure AD, Auth0 or IdentityServer. See
[design decisions](#3-real-jwt-validation-stand-in-issuer) for what production
would change.

**Demo credentials:** `demo` / `Password123!`

### Swagger

1. Open <http://localhost:8080/swagger>
2. `POST /api/auth/token`, Try it out, Execute
3. Copy `accessToken` from the response
4. Click Authorize, paste the token
5. The secured endpoints now work

### curl

```bash
TOKEN=$(curl -s -X POST http://localhost:8080/api/auth/token \
  -H 'Content-Type: application/json' \
  -d '{"username":"demo","password":"Password123!"}' | jq -r .accessToken)

curl http://localhost:8080/api/products -H "Authorization: Bearer $TOKEN"
curl "http://localhost:8080/api/products?colour=Red" -H "Authorization: Bearer $TOKEN"
```

### Front end

The sign-in panel is pre-filled with the demo credentials, calls the same token
endpoint and stores the result, so the auth wiring is visible without a real login
flow.

## API reference

Responses are JSON; errors are [RFC 7807](https://datatracker.ietf.org/doc/html/rfc7807)
`ProblemDetails`.

| Method | Route | Auth | Purpose |
|---|---|:---:|---|
| `GET` | `/health` | | Readiness, includes a database probe |
| `GET` | `/health/live` | | Liveness, excludes the database |
| `POST` | `/api/auth/token` | | Issue a demo bearer token |
| `GET` | `/api/auth/me` | ✓ | Echo the caller's claims |
| `GET` | `/api/products` | ✓ | List, paged and sorted |
| `GET` | `/api/products?colour=Red` | ✓ | Filter by colour |
| `GET` | `/api/products/colour/{colour}` | ✓ | Filter by colour, route form |
| `GET` | `/api/products/{id}` | ✓ | Fetch one |
| `POST` | `/api/products` | ✓ | Create, returns 201 + `Location` |
| `PUT` | `/api/products/{id}` | ✓ | Update |
| `DELETE` | `/api/products/{id}` | ✓ | Delete, returns 204 |

Query parameters: `page` (default 1), `pageSize` (default 20, max 100), `sortBy`
(`name`, `price`, `colour`, `sku`, `createdAt`, `updatedAt`), `sortDescending`,
`colour`.

`/api/products` and `/api/v1/products` both route to the same actions, so the
unversioned contract from the brief keeps working alongside an explicit version.
Versions can also be selected with the `X-Api-Version` header or an `api-version`
query parameter.

| Code | When |
|---|---|
| `200` / `201` / `204` | Success. Create returns 201 with a `Location` header |
| `400` | Validation failed; the body lists every failing field |
| `401` | Missing, malformed, expired or wrongly-signed token |
| `404` | No such product |
| `409` | SKU already in use |
| `429` | Rate limited; carries `Retry-After` |

## Tests

```bash
dotnet test                # 187 backend tests
cd frontend && npm test    # 45 front-end tests
```

With coverage:

```bash
dotnet test --collect:"XPlat Code Coverage"
cd frontend && npm run test:coverage
```

| Suite | Tests | Covers |
|---|---:|---|
| `Products.Domain.Tests` | 59 | Invariants, value objects, domain events, boundaries |
| `Products.Application.Tests` | 74 | Handlers, validators, behaviours, paging |
| `Products.Api.IntegrationTests` | 54 | Full HTTP pipeline over real SQL, both sides of auth |
| `frontend` | 45 | Form validation, table, filter, API client, app states |

Backend line coverage is around 78%, front end around 83%. The edge cases the
brief called out each have a named test (invalid colour, empty name, negative
price, empty result set), along with ones that bite in practice: differently-cased
SKUs colliding as duplicates, and pages not overlapping when the sort key is not
unique.

Integration tests run against SQLite rather than EF's InMemory provider. InMemory
ignores unique indexes, column lengths and SQL translation, so the duplicate-SKU
constraint and the colour-filter query would both go untested.

CI runs all four suites plus a type-check and both Docker builds on every push and
pull request: [`.github/workflows/ci.yml`](.github/workflows/ci.yml).

## Repository layout

```
├── src/
│   ├── Products.Domain/          entities, value objects, domain events; no dependencies
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

## Design decisions

### 1. Clean Architecture

For a service this size the layering is more structure than the feature set
needs. It earns its place because the brief is about a microservice in a
distributed system: the domain and application layers do not know whether they
are driven by HTTP, a message consumer or a background job, so adding a broker
consumer later is a new entry point rather than a rewrite.

The practical result is that `Products.Domain.csproj` is empty, which makes the
dependency rule a compiler concern rather than a review concern.

### 2. MediatR 12.5.0 and FluentAssertions 7.x

Both libraries moved to paid commercial licences in later majors (MediatR at 13,
FluentAssertions at 8). Both are pinned here to the last release under Apache 2.0,
through Central Package Management.

Keeping every version in `Directory.Packages.props` also means version drift
between projects is not possible.

### 3. Real JWT validation, stand-in issuer

Validation is production-shaped: issuer, audience, signing key and lifetime are
all checked, clock skew is reduced from the default five minutes to thirty
seconds, and the signing algorithm is pinned, since leaving it open is the basis
of `alg` confusion attacks.

The issuer is what is fake. Production would use an external IdP with asymmetric
signing (RS256) and keys published via JWKS, so the API would hold a verification
key only. The token endpoint would not exist.

The development signing key is committed in `appsettings.Development.json` so the
repository clones and runs. It is only loaded by the Development environment, and
there is no production fallback: options are validated at start-up, so a
deployment without `Jwt__SigningKey` fails to boot.

### 4. SQLite by default, SQL Server by configuration

The provider is configuration, not code, so switching is a connection string and
one setting. SQLite is the default because it keeps `docker compose up` to a
single command with no external dependency.

The caveat: the committed migration set targets SQLite. EF Core migrations are
provider-specific, so SQL Server needs its own:

```bash
dotnet ef migrations add InitialCreate \
  --project src/Products.Infrastructure \
  --output-dir Persistence/Migrations/SqlServer
```

### 5. Timestamps stored as UTC `DateTime`

SQLite rejects `ORDER BY` on a `DateTimeOffset` column, and `createdAt` is the
default sort, so the most common query in the service returned a 500. Found by
running it, not by reading it.

The fix is a value converter storing UTC `DateTime`. Nothing is lost, since every
timestamp comes from `TimeProvider.GetUtcNow()`. It is applied to both providers
so that ordering cannot behave differently in development and production.

### 6. Rate limits on writes and auth only

Reads are cheap and idempotent; writes cost a database round trip, and the token
endpoint is the target for credential stuffing. Throttling reads would degrade the
front end for no benefit.

Buckets are per authenticated user, falling back to remote IP. That fallback is
only correct if the address is the client's, and behind a load balancer it is the
proxy's, which would put every anonymous user in one bucket. Hence
`UseForwardedHeaders`, with `KnownProxies` left empty and commented, because
clearing it without naming the real proxy lets a client spoof `X-Forwarded-For`.

### 7. Controllers rather than minimal APIs

Minimal APIs would also work. Controllers were chosen because attribute routing,
API versioning, `[Authorize]`, per-endpoint rate limiting and Swagger's XML
documentation compose more cleanly through attributes.

### 8. Hand-written mapping

`ProductDto.FromEntity` is a few assignments the compiler checks. A mapping
library would add a dependency and move those errors to run time to save about
fifteen lines.

### 9. Value objects for SKU and Money

A bare `string Sku` lets `"abc-1"` and `"ABC-1"` become two rows behind a unique
index. A bare `decimal Price` leaves the "not negative" rule wherever someone
remembers to put it. `Money` also binds an amount to its currency, so adding GBP
to USD stops being expressible.

### 10. `localStorage` for the demo token

A production SPA should not keep a bearer token in `localStorage`, since anything
running on the page can read it and an XSS becomes credential theft. The usual
answer is a short-lived token in memory with a refresh token in an `HttpOnly`
cookie.

It is used here because the demo has no refresh flow and losing the session on
reload would get in the way of review. Flagged in the code as well.

## Known gaps

**Not built**

- No broker integration. Domain events are raised and dispatched in-process; the
  outbox and relay are described in `docs/architecture.md` but not implemented.
  The extension point is a single event handler.
- No SQL Server migration set (see decision 4).
- Authentication without authorisation. Every valid token can do everything. A
  role claim is issued but unused.
- No refresh tokens or revocation. The short token lifetime is the mitigation.

**Next steps, roughly in order**

1. Outbox and relay, publishing `ProductCreated` and `ProductPriceChanged` to
   RabbitMQ. The highest-value addition, and the one the architecture is shaped
   around.
2. OpenTelemetry tracing propagated through message headers, extending the
   existing `traceId` across services.
3. Optimistic concurrency on update. Two concurrent `PUT`s currently
   last-write-wins silently; a `rowversion` and a 409 would surface the conflict.
4. Kubernetes manifests using the existing liveness and readiness probes, with
   migrations as a pre-deploy job so replicas do not race to migrate one schema.
5. Cursor-based pagination alongside offset. `OFFSET` degrades on large tables and
   can skip rows when data shifts between requests; the ordering already has a
   unique tiebreaker to make a cursor straightforward.
6. A real IdP, retiring `/api/auth/token`.

**Left out on purpose**

- No caching layer. Premature without a measured read pattern, and invalidation on
  a catalogue that publishes change events needs a design.
- No generic repository base class. Two repositories do not justify it.
- No CSP header on the front end. A policy written without knowing the real asset
  origins either does nothing or breaks the page.

## Licence

[MIT](LICENSE)
