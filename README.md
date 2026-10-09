# Ticket System (.NET)

A support-ticket REST API in **C# / ASP.NET Core 10** with **Entity Framework Core** on **SQL Server**, JWT authentication, role-based access, xUnit tests and a small **Angular** front end.

It is a port of my Spring Boot ticket system to the .NET stack, rewritten to use EF Core idioms (LINQ projections, migrations, a stored procedure) rather than translated line by line.

| Layer | Technology |
|---|---|
| API | ASP.NET Core 10 Web API (controllers), async/await throughout |
| Data | EF Core 10, SQL Server 2022, code-first migrations, one stored procedure |
| Auth | JWT bearer tokens, BCrypt password hashes, role-based `[Authorize]` |
| Tests | xUnit: unit tests on SQLite in-memory, integration tests over HTTP on SQL Server |
| Front end | Angular 22 (standalone components, signals), Vitest |
| Delivery | Docker Compose, GitHub Actions CI, Azure App Service deploy workflow |

## Run it

Everything (SQL Server, API, Angular app) with one command. Needs Docker only:

```bash
docker compose up --build
```

- App: http://localhost:8080
- Swagger UI: http://localhost:8080/swagger
- API directly: http://localhost:5080

On first start the API applies the EF Core migrations (tables + stored procedure) and creates these demo accounts:

| Role | Email | Password |
|---|---|---|
| Admin | admin@company.com | Admin@123 |
| Agent | agent@company.com | Agent@123 |
| Developer | dev1@company.com | Dev@123 |
| Developer | dev2@company.com | Dev@123 |
| Client (Acme) | client1@acme.com | Client@123 |
| Client (TechStart) | client2@techstart.io | Client@123 |

### Without Docker for the app

Start only the database with `docker compose up sql`, then:

```bash
dotnet run --project src/TicketSystem.Api     # http://localhost:5080, Development settings
cd client && npm install && npm start         # http://localhost:4200, proxies /api to :5080
```


## Tests

```bash
dotnet test                                   # unit tests; integration tests are skipped
```

To run the integration tests too, give them a SQL Server (each run creates and drops its own database):

```bash
docker compose up -d sql
TEST_SQLSERVER="Server=localhost,1433;User Id=sa;Password=Your_strong_Passw0rd;TrustServerCertificate=True" dotnet test
```

```bash
cd client && npx ng test --watch=false        # Angular specs (Vitest)
```

Current result: 26 .NET tests (19 unit, 7 integration) and 7 Angular tests, all passing.

## API

All endpoints except login need `Authorization: Bearer <token>` from `POST /api/auth/login`.

| Method | Endpoint | Who |
|---|---|---|
| POST | `/api/auth/login` | anyone |
| GET | `/api/auth/me` | signed in |
| GET | `/api/tickets?status=&priority=&category=&clientId=&assigneeId=&page=0&size=20&sortBy=createdAt&sortDir=desc` | signed in, filtered by role |
| POST | `/api/tickets` | Admin, Agent, Client |
| GET / PUT | `/api/tickets/{id}` | anyone who can see the ticket |
| PUT | `/api/tickets/{id}/assign` | Admin, Agent |
| DELETE | `/api/tickets/{id}` | Admin |
| GET / POST | `/api/tickets/{id}/comments` | anyone who can see the ticket |
| DELETE | `/api/tickets/{id}/comments/{commentId}` | comment author or Admin |
| GET / POST / PUT | `/api/clients`, `/api/clients/{id}` | Admin, Agent |
| GET / POST / PUT | `/api/users`, `/api/users/{id}` | Admin |
| GET | `/api/dashboard/stats` | Admin, Agent |
| GET | `/health` | anyone (checks the database) |

### Who sees what

- **Clients** see and edit (title, description) only their own company's tickets, and never see internal comments.
- **Developers** see only tickets assigned to them and can change their status.
- **Agents and admins** see everything, triage (priority, category) and assign. Only admins delete.

### Errors

Every error has the same JSON shape and status code:

```json
{ "timestamp": "2026-10-08T14:41:52Z", "status": 403, "error": "Forbidden",
  "message": "You are not allowed to view this ticket", "path": "/api/tickets/…" }
```

| Status | When |
|---|---|
| 400 | validation failed (adds `fieldErrors`) or a business rule was broken |
| 401 | missing/expired token, or wrong email/password |
| 403 | signed in but not allowed |
| 404 | the ticket, user, client or comment doesn't exist |
| 409 | email already in use |

## How it's built

```
src/TicketSystem.Api/
  Controllers/   thin HTTP layer: routing, [Authorize] roles, status codes
  Services/      business rules (who can do what), LINQ queries
  Data/          AppDbContext, migrations, seed data
  Models/        EF Core entities and enums
  Dtos/          request/response records with validation attributes
  Auth/          JWT creation, CurrentUser from claims
  Errors/        exceptions → consistent JSON errors (IExceptionHandler)
tests/TicketSystem.Api.Tests/
  Unit/          services against SQLite in-memory
  Integration/   WebApplicationFactory + real SQL Server
client/          Angular app
```

A few decisions worth pointing out:

- **No N+1 queries.** Ticket lists are a single `SELECT` via a LINQ projection (`Mapping.ToTicketResponse`), including each ticket's comment count as a subquery. The Java version ran one extra count query per row.
- **Stored procedure for the dashboard.** `dbo.usp_TicketCounts` (created in the `AddTicketCountsProcedure` migration) groups tickets by status and priority in one round trip. It's called with `Database.SqlQuery<T>($"EXEC …")`.
- **Sorting by severity.** Enums are stored as readable strings, so ordering by the column would be alphabetical (Critical, High, Low, Medium). Sorting maps them to a SQL `CASE` rank instead.
- **UTC everywhere.** Timestamps are set in `SaveChangesAsync` and read back as UTC, so the JSON ends in `Z` and the browser shows local time correctly.
- **Services take a `CurrentUser`**, not `HttpContext`, so the access rules are unit-tested without a web server.
- **Whitelisted sort fields**, so the query string can't order by arbitrary columns.

### Troubleshooting with logs

The API logs through `ILogger`: ticket creation, assignment and deletion at `Information`, every 4xx at `Warning` with method, path and reason, and 5xx at `Error` with the stack trace. In Development, EF Core also logs each SQL command it runs.

```bash
docker compose logs -f api
```

## Deploying to Azure

`.github/workflows/deploy-azure.yml` publishes the API to an Azure App Service (Linux, .NET 10) when run manually from the Actions tab. To use it:

1. Create an App Service and an Azure SQL database.
2. In the App Service's configuration set `ConnectionStrings__Default`, `Jwt__Key` (32+ characters) and, for the first deploy, `Database__MigrateOnStartup=true`.
3. In the GitHub repo add the variable `AZURE_WEBAPP_NAME` and the secret `AZURE_WEBAPP_PUBLISH_PROFILE` (downloaded from the App Service).
4. Point the App Service health check at `/health`, and read logs from the App Service log stream.

The Angular build (`client/dist/client/browser`) can go to Azure Static Web Apps with `/api` routed to the App Service.

This workflow has not been run against a live Azure subscription yet.
