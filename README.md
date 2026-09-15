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

### ✅ Phase 2 — Booking & Pricing Engine (done)
- ✅ `CalculatePriceService` implementing time-of-day pricing rules:
  - Rules (time range + multiplier) are loaded from `appsettings.json` (`PricingRules`), not
    hardcoded — currently configured for the assignment's bands (06:00–09:00 ×0.90,
    12:00–14:00 ×1.15, 18:00–23:00 ×0.80, everything else ×1.0)
  - Bookings spanning multiple pricing bands (e.g. 11:00–15:00) are prorated hour-by-hour
    rather than priced at a single rate
  - Registered in DI (`Program.cs`) via a factory that binds `PricingRuleConfig` from config
  - Each segment is bounded by the nearest of `+1 hour`, the next pricing-band start/end, or the
    booking's end — so bookings that don't start exactly on the hour (e.g. `11:30–13:30`) are
    still prorated correctly across a band boundary (verified: `11:30–13:30` at base rate 100
    with the 12:00–14:00 ×1.15 band → 50 + 172.5 = 222.5)
  - **Known limitation:** a pricing rule that wraps past midnight (e.g. `23:00–02:00`) would
    never match, since the comparison assumes `StartTime < EndTime`. Not an issue with the
    current `PricingRules` config, but would need explicit handling if such a rule is added.
  - A temporary `GET /api/Test/price?time=10:00-14:00&price=100` endpoint
    (`Controllers/TestController.cs`) was used during development to manually exercise the
    service end-to-end; it has since been removed now that the pricing service is exercised
    through the real booking flow (Phase 2 completed, see Code Style Review below).
- ✅ `Booking` entity and `CreateBookingCommand` CQRS command/handler to book a hall (hall id,
  start date/time, duration, selected services), with:
  - `POST /api/Bookings` — creates the booking and returns a confirmation with the calculated
    total cost
  - `GET /api/Bookings` — lists all bookings together with the services booked on each one
  - Overlap prevention: a new booking is rejected (`409 Conflict`) if it overlaps an existing,
    non-removed booking for the same hall; back-to-back bookings (one ending exactly when the
    next starts) are allowed
  - A per-hall in-process lock (`SemaphoreSlim` keyed by `HallId`) serializes the
    check-then-insert sequence so concurrent requests for the same hall/slot can't both pass the
    overlap check and double-book it (verified with 10 concurrent requests for the same slot: 1
    succeeds, 9 get `409`). This only protects a single running instance — horizontally scaling
    to multiple instances would need a DB-level constraint or distributed lock instead.
  - Requested services are validated against the services actually offered by the target hall
    (`400` if a service doesn't belong to that hall), and each booked service is snapshotted into
    a `ServiceFreezeEntity` (`OriginalId`, `Name`, `Price`, `FreezeDate`) so a later price change
    on the live service doesn't retroactively affect past bookings
  - `Duration` must be positive and `StartDate` cannot be in the past (`400` otherwise)
  - Dates are accepted and returned in `dd-MM-yyyy HH:mm` format (custom
    `JsonConverter<DateTime>` in `Common/DdMmYyyyDateTimeConverter.cs`), not the default ISO 8601
  - Prerequisite for Phase 3, since availability search needs the `Booking` entity to check
    existing reservations against

### ✅ Phase 3 — Availability Search (done)
- `GET /api/Halls/search` — `SearchAvailableHallsQuery`/`SearchAvailableHallsQueryHandler`
  (`Handlers/SearchAvailableHallsQueryHandler.cs`), returns halls matching a date, time range
  and required capacity:
  - Filters out soft-deleted halls and any hall whose `Capacity` is below the requested value
  - Cross-references `Bookings` (Phase 2) with a `NOT EXISTS`-style subquery, excluding halls that
    have a non-removed booking overlapping `[startDate, endDate)` — the same overlap predicate
    (`b.StartDate < endDate && b.EndDate > startDate`) already used by
    `CreateBookingCommandHandler`, so a search and an actual booking attempt agree on what counts
    as "available"; back-to-back slots (one ending exactly when the requested range starts, or
    vice versa) are **not** treated as a conflict
  - `startDate`/`endDate` are query-string parameters in `dd-MM-yyyy HH:mm` format, parsed with
    the same `DdMmYyyyDateTimeConverter.Format` constant used for booking bodies (now `public` so
    it can be shared) — since ASP.NET Core's default query-string binder doesn't go through
    `JsonConverter`, the query is bound as raw strings and parsed manually
  - Validates `endDate > startDate` and `capacity > 0`, returning `400` with a descriptive message
    otherwise (same `ArgumentException` → `BadRequest` pattern as `BookingsController`)
  - Verified manually end-to-end: capacity filtering (`>=` boundary included, one above excluded),
    touching-but-not-overlapping ranges included, an exact-match range excluded, invalid date
    format / inverted range / non-positive capacity all return `400`, soft-deleted halls never
    appear, and a soft-deleted (`Removed = true`) booking no longer blocks its hall

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

### ✅ Phase 9 — Documentation & Repository Setup (done)
- This README
- Handlers already contain inline comments explaining non-obvious logic (soft-delete
  cascades, service reconciliation on update) — partially covers the "code comments" bonus item
- Git repository initialized in the project folder, with history tracking each delivery phase

### ✅ Code Style Review (done)
A pass over the existing codebase to clean up comments and remove dev-only leftovers, ahead of
further feature work:
- All in-code comments and controller-facing message strings translated from Ukrainian to English
  for a consistent codebase language
- Expanded inline comments on non-obvious logic that previously had none or only a short note,
  including: why the query-string date parsing in `SearchAvailableHallsQueryHandler` bypasses
  `DdMmYyyyDateTimeConverter`, the interval-overlap predicate shared between booking creation and
  availability search, the segment-by-segment walk in `CalculatePriceService.Calculate`, the
  per-hall `SemaphoreSlim` lock's single-instance-only guarantee, why halls/services are soft- not
  hard-deleted, why `ServiceEntity.Hall` is a many-to-many navigation despite each service
  belonging to one hall in practice, and why the pricing service is registered `Transient` instead
  of `Singleton`
- Removed `Controllers/TestController.cs`, the temporary `GET /api/Test/price` endpoint used to
  manually exercise `CalculatePriceService` during Phase 2 development (see Phase 2 above) — no
  longer needed now that pricing is exercised through the real booking flow

## API Endpoints (current)

| Method | Route              | Description                                  |
|--------|---------------------|-----------------------------------------------|
| GET    | `/api/Halls`         | List all active halls with their services     |
| GET    | `/api/Halls/{id}`    | Get a hall by id                               |
| GET    | `/api/Halls/search`  | Search halls by date, time range and capacity, excluding already-booked halls |
| POST   | `/api/Halls`         | Create a new hall                              |
| PUT    | `/api/Halls/{id}`    | Update a hall and its services                 |
| DELETE | `/api/Halls/{id}`    | Soft-delete a hall and its services            |
| GET    | `/api/Bookings`       | List all bookings with their booked services   |
| POST   | `/api/Bookings`       | Create a booking for a hall (+ optional services) |

## Request/Response Payloads

This section explains, endpoint by endpoint, exactly what to send and what you get back.

> **⚠️ Date format:** every date (`startDate` / `endDate`) is written and read as
> `dd-MM-yyyy HH:mm` — for example `20-09-2026 14:00`.
> This is **not** the standard ISO 8601 format, and sending ISO 8601 will fail with `400 Bad Request`.

---

### 1. `GET /api/Halls` — list all halls

**What it does:** returns every active hall together with its active services. Use this to show clients what's available.

**Send:** nothing (no body, no parameters).

**You get back:**
```json
[
  {
    "id": "c1d1a2b3-0000-0000-0000-000000000001",
    "name": "Hall A",
    "capacity": 50,
    "price": 100.0,
    "removed": false,
    "services": [
      { "id": "5e10a1b2-0000-0000-0000-000000000001", "name": "Projector", "price": 20.0, "removed": false }
    ]
  }
]
```

---

### 2. `GET /api/Halls/{id}` — get one hall

**What it does:** returns the details of a single hall.

**Send:** the hall's `id` as part of the URL, e.g. `GET /api/Halls/c1d1a2b3-0000-0000-0000-000000000001`.

**You get back:** the same object shape as one item from endpoint 1.

**If the hall doesn't exist:** `404 Not Found`.

---

### 3. `POST /api/Halls` — create a hall

**What it does:** registers a new hall, optionally with the list of services it offers right away.

**Send this JSON body:**
```json
{
  "name": "Hall A",
  "capacity": 50,
  "price": 100.0,
  "services": [
    { "name": "Projector", "price": 20.0 },
    { "name": "Wi-Fi", "price": 5.0 }
  ]
}
```

| Field                | Required | What it means                          |
|----------------------|----------|------------------------------------------|
| `name`               | ✅ yes    | Hall name shown to clients                |
| `capacity`           | ✅ yes    | Max number of people the hall fits        |
| `price`              | ✅ yes    | Base rental price per hour                |
| `services`           | optional | List of extra services this hall offers   |
| `services[].name`    | ✅ yes*   | Service name (e.g. "Projector")           |
| `services[].price`   | ✅ yes*   | Price of that service                     |

\* only required for each service you include in the list.

**You get back:** `200 OK` with the newly created hall (including its generated `id`).

---

### 4. `PUT /api/Halls/{id}` — update a hall

**What it does:** updates a hall's details and re-syncs its list of services in one call.

**Send:** the hall's `id` in the URL, plus this JSON body:
```json
{
  "name": "Hall A",
  "capacity": 60,
  "price": 120.0,
  "services": [
    { "id": "5e10a1b2-0000-0000-0000-000000000001", "name": "Projector", "price": 25.0 },
    { "id": null, "name": "Sound System", "price": 30.0 }
  ]
}
```

| Field               | Required | What it means                                                                 |
|---------------------|----------|----------------------------------------------------------------------------------|
| `name`              | ✅ yes    | New hall name                                                                    |
| `capacity`          | ✅ yes    | New capacity                                                                     |
| `price`             | ✅ yes    | New base hourly price                                                           |
| `services`          | optional | The **full, final** list of services this hall should have                      |
| `services[].id`     | optional | Set to an existing service's `id` to update it, or `null` to create a new one    |
| `services[].name`   | ✅ yes*   | Service name                                                                     |
| `services[].price`  | ✅ yes*   | Service price                                                                    |

\* only required for each service you include in the list.

> 💡 **How service syncing works:** whatever you put in `services` becomes the hall's new service list.
> Any service the hall had *before* that is missing from this array gets removed (soft-deleted).

**You get back:** `200 OK` on success, or `404 Not Found` if the hall doesn't exist.

---

### 5. `DELETE /api/Halls/{id}` — remove a hall

**What it does:** soft-deletes the hall and all of its services (they're hidden, not physically erased, so past bookings stay intact).

**Send:** just the hall's `id` in the URL. No body.

**You get back:** `200 OK` on success, `404 Not Found` if the hall doesn't exist.

---

### 6. `GET /api/Bookings` — list all bookings

**What it does:** returns every booking made so far, with its services and total price.

**Send:** nothing (no body, no parameters).

**You get back:**
```json
[
  {
    "id": "b0000000-0000-0000-0000-000000000001",
    "hallId": "c1d1a2b3-0000-0000-0000-000000000001",
    "hallName": "Hall A",
    "startDate": "20-09-2026 10:00",
    "endDate": "20-09-2026 12:00",
    "totalPrice": 240.0,
    "services": [
      { "name": "Projector", "price": 20.0 }
    ]
  }
]
```

---

### 7. `POST /api/Bookings` — create a booking

**What it does:** books a hall for a specific date/time and duration, with an optional list of services. The price is calculated automatically from the hall's base price and the time-of-day pricing rules.

**Send this JSON body:**
```json
{
  "hallId": "c1d1a2b3-0000-0000-0000-000000000001",
  "startDate": "20-09-2026 10:00",
  "duration": "02:00:00",
  "serviceIds": [
    "5e10a1b2-0000-0000-0000-000000000001"
  ]
}
```

| Field        | Required | What it means                                                                 |
|--------------|----------|-----------------------------------------------------------------------------------|
| `hallId`     | ✅ yes    | `id` of the hall you want to book                                                 |
| `startDate`  | ✅ yes    | Booking start, format `dd-MM-yyyy HH:mm`. Cannot be a date/time in the past        |
| `duration`   | ✅ yes    | How long the booking lasts, format `HH:mm:ss` (e.g. `"02:00:00"` = 2 hours). Must be greater than zero |
| `serviceIds` | optional | `id`s of the services to add — each one must belong to the chosen hall            |

**You get back** (`200 OK`) a confirmation with the calculated price:
```json
{
  "bookingId": "b0000000-0000-0000-0000-000000000001",
  "hallName": "Hall A",
  "startDate": "20-09-2026 10:00",
  "endDate": "20-09-2026 12:00",
  "totalPrice": 240.0,
  "services": [
    { "name": "Projector", "price": 20.0 }
  ]
}
```

**What can go wrong:**

| Status | Why                                                                                     |
|--------|-------------------------------------------------------------------------------------------|
| `400`  | Bad input — duration isn't positive, start date is in the past, or a service doesn't belong to this hall |
| `404`  | The hall doesn't exist                                                                     |
| `409`  | The requested time slot overlaps with an existing booking for the same hall                |

### 8. `GET /api/Halls/search` — search available halls

**What it does:** returns active halls that fit the requested capacity **and** have no booking
overlapping the requested date/time range. Use this before `POST /api/Bookings` to only offer
clients halls that are actually bookable.

**Send:** query-string parameters, no body, e.g.
`GET /api/Halls/search?startDate=20-09-2026 10:00&endDate=20-09-2026 14:00&capacity=50`

| Parameter   | Required | What it means                                                        |
|-------------|----------|------------------------------------------------------------------------|
| `startDate` | ✅ yes    | Start of the requested slot, format `dd-MM-yyyy HH:mm`                 |
| `endDate`   | ✅ yes    | End of the requested slot, format `dd-MM-yyyy HH:mm`. Must be after `startDate` |
| `capacity`  | ✅ yes    | Minimum number of people the hall must fit. Must be greater than zero  |

**You get back:** the same array shape as endpoint 1 (`GET /api/Halls`), containing only halls
that match the capacity and are free for the whole `[startDate, endDate)` window. A hall with a
booking that ends exactly at `startDate`, or starts exactly at `endDate`, is still considered free.

```json
[
  {
    "id": "c1d1a2b3-0000-0000-0000-000000000001",
    "name": "Hall A",
    "capacity": 50,
    "price": 100.0,
    "removed": false,
    "services": [
      { "id": "5e10a1b2-0000-0000-0000-000000000001", "name": "Projector", "price": 20.0, "removed": false }
    ]
  }
]
```

**What can go wrong:**

| Status | Why                                                                                     |
|--------|-------------------------------------------------------------------------------------------|
| `400`  | `startDate`/`endDate` isn't in `dd-MM-yyyy HH:mm` format, `endDate` isn't after `startDate`, or `capacity` isn't a positive number |

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
  updated to reflect the real `Halls`/`Bookings` endpoints.
- The double-booking guard (`Handlers/CreateBookingHandlerCommand.cs`) uses an in-process
  per-hall lock, not a database-level constraint — it prevents races within a single running
  instance but not across multiple instances behind a load balancer.
- Booking dates are parsed strictly as `dd-MM-yyyy HH:mm`; the default ASP.NET Core model binder
  will reject any other format (including ISO 8601) with a `400`.
- There's no endpoint to cancel/soft-delete a booking yet, so `GET /api/Halls/search`'s handling
  of a soft-deleted booking (it should stop excluding the hall) was verified by flipping the
  `Removed` flag directly in the SQLite database, not through the API.
