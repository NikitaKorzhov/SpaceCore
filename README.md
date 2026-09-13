# SpaceCore

A backend API for managing conference hall bookings and rental cost calculation, built with ASP.NET Core.

## Business Context

A company rents out conference halls to businesses. Clients need to be able to:
- Search for halls that are available for a given date, time range and capacity.
- Book a hall together with optional extra services (projector, Wi-Fi, sound, etc.).
- See the total rental cost calculated automatically, based on time-of-day pricing rules.

Hall managers need to be able to:
- Add new halls with a base hourly price and a list of available services.
- Edit hall details (price, capacity, services) as the offering evolves.
- Remove halls that are no longer rented out.
- Access business reports (revenue, occupancy, popular services) to support decision-making.

## Tech Stack

- **.NET 10 / ASP.NET Core Web API**
- **Entity Framework Core** with **SQLite** as the data store
- **MediatR** — CQRS-style command/query separation between controllers and business logic
- Native **Microsoft.AspNetCore.OpenApi** for API document generation

## Architecture

The solution follows a lightweight CQRS structure:

```
Controllers/   → HTTP endpoints, request/response mapping only
Handlers/      → MediatR commands, queries and their handlers (business logic)
DTOs/          → Input/output contracts, decoupled from EF entities
Models/        → EF Core entities
Data/          → DbContext and persistence configuration
Migrations/    → EF Core migration history
```

Each use case (create hall, update hall, delete hall, etc.) is implemented as a MediatR
`IRequest`/`IRequestHandler` pair, keeping controllers thin and business rules testable in
isolation. Halls and Services are soft-deleted (`Removed` flag) rather than physically
deleted, preserving historical data for future reporting and bookings.

## Implementation Phases

Progress against the technical assignment, split into delivery phases.

### ✅ Phase 0 — Project Foundation (done)
- ASP.NET Core Web API project scaffolded (.NET 10)
- EF Core + SQLite configured, connection string externalized to `appsettings.json`
- MediatR wired up for CQRS-style request handling
- Initial database migrations created

### ✅ Phase 1 — Hall Management (done)
- `POST /api/Halls` — create a hall with name, capacity, hourly price and a list of services
- `GET /api/Halls` — list all active halls with their active services
- `GET /api/Halls/{id}` — get a single hall by id
- `PUT /api/Halls/{id}` — update hall details, add/update/remove services
- `DELETE /api/Halls/{id}` — soft-delete a hall and its services
- Soft-delete implemented for both halls and services

### ⏳ Phase 2 — Booking & Pricing Engine (not started)
- `Booking` entity and CQRS command to book a hall (hall id, date/time, duration, selected services)
- Time-based pricing engine implementing the rules from the assignment:
  - Standard hours (09:00–18:00): base price
  - Evening hours (18:00–23:00): 20% discount
  - Morning hours (06:00–09:00): 10% discount
  - Peak hours (12:00–14:00): 15% surcharge
  - Needs a defined strategy for bookings that span multiple pricing bands (e.g. 11:00–15:00
    crosses standard and peak hours)
- Booking confirmation response including the calculated total cost
- Prerequisite for Phase 3, since availability search needs the `Booking` entity to check
  existing reservations against

### ⏳ Phase 3 — Availability Search (not started)
- `GET` endpoint to search halls by date, time range and required capacity
- Requires cross-referencing existing bookings (Phase 2) to exclude halls that are already
  booked for the requested slot

### ⏳ Phase 4 — Reports & Analytics (not started, optional)
Business-facing reports were requested but not yet designed/implemented. Candidates:
- Revenue by hall / by period
- Hall occupancy rate
- Most frequently booked services
- Booking volume trends over time

### 🟡 Phase 5 — API Documentation (partially done)
- OpenAPI document generation (`/openapi/v1.json`) is enabled in the Development environment
- Interactive Swagger UI page is not yet wired up
- Endpoint summaries, examples and response types are not yet documented via XML comments/attributes

### ⏳ Phase 6 — Validation, Error Handling & Logging (not started)
- No request validation on DTOs (e.g. required fields, positive prices/capacity)
- No global exception-handling middleware / consistent error response shape
- No structured application logging

### ⏳ Phase 7 — Security Hardening (not started)
- No authentication/authorization on any endpoint
- No rate limiting
- No CORS policy defined
- HTTPS redirection is enabled, but production hardening (headers, secrets management) is outstanding

### ⏳ Phase 8 — Testing (not started)
- No automated tests yet (unit tests for the pricing engine and handlers, integration tests
  for controllers)

### 🟡 Phase 9 — Documentation & Repository Setup (in progress)
- This README
- Handlers already contain inline comments explaining non-obvious logic (soft-delete
  cascades, service reconciliation on update) — partially covers the "code comments" bonus item
- Git repository not yet initialized in the project folder

## API Endpoints (current)

| Method | Route              | Description                                  |
|--------|---------------------|-----------------------------------------------|
| GET    | `/api/Halls`         | List all active halls with their services     |
| GET    | `/api/Halls/{id}`    | Get a hall by id                               |
| POST   | `/api/Halls`         | Create a new hall                              |
| PUT    | `/api/Halls/{id}`    | Update a hall and its services                 |
| DELETE | `/api/Halls/{id}`    | Soft-delete a hall and its services            |

Booking and availability-search endpoints are planned (see Phase 2 and Phase 3).

## Getting Started

**Prerequisites:** .NET 10 SDK

```bash
# Restore dependencies
dotnet restore

# Apply database migrations (creates spacecore.db)
dotnet ef database update

# Run the API
dotnet run
```

The API listens on `http://localhost:5041` (see `Properties/launchSettings.json`).
In the Development environment, the raw OpenAPI document is available at `/openapi/v1.json`.

## Known Limitations

- Seed data for the initial halls/services from the assignment (Hall A/B/C, Projector/Wi-Fi/Sound)
  is not pre-populated in the database — halls must be created via the API.
- `SpaceCore.http` still contains the default project template request and has not been
  updated to reflect the real `Halls` endpoints.
