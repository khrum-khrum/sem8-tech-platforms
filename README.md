# CdrBilling

High-performance ASP.NET Core 10 backend for processing telecom **Call Detail Records (CDR)**, applying tariffs, and producing billing summaries. Includes a React web UI for end-to-end interaction.

---

## Table of Contents

- [Overview](#overview)
- [Architecture](#architecture)
- [Prerequisites](#prerequisites)
- [Quick Start](#quick-start)
- [Configuration](#configuration)
- [Input File Formats](#input-file-formats)
- [API Reference](#api-reference)
- [Tariffication Rules](#tariffication-rules)
- [End-to-End Usage Example](#end-to-end-usage-example)
- [Project Structure](#project-structure)
- [Performance Design](#performance-design)
- [Database Migrations](#database-migrations)

---

## Overview

CdrBilling implements a session-based tariffication workflow:

1. Create a billing session.
2. Upload three input files: CDR records, tariff table, subscriber list.
3. Trigger tariffication — the backend runs asynchronously and streams live progress over SSE.
4. Query billing summaries and paginated call detail records.

The system is built for **large file volumes** (up to 2 GB per upload) with zero-copy streaming ingestion and O(k) per-call tariff lookup via an in-memory prefix trie.

---

## Architecture

Clean architecture with four projects:

```
CdrBilling.Domain         — Entities, enums, prefix trie, tariffication engine (no framework deps)
CdrBilling.Application    — MediatR CQRS handlers, repository interfaces, DTOs
CdrBilling.Infrastructure — EF Core (writes) + Dapper (reads) + Npgsql binary COPY + SSE hub
CdrBilling.Api            — ASP.NET Core Minimal API endpoints, DI composition root
web/                      — React + TypeScript frontend (Vite)
```

### Data flow

```
HTTP multipart upload
        │
        ▼
System.IO.Pipelines (zero-copy line reader)
        │
        ▼
Domain entities (CallRecord / TariffEntry / Subscriber)
        │
        ▼
Npgsql binary COPY → PostgreSQL 16
        │
        ▼
TarificationEngine (prefix trie lookup, per-call charge)
        │
        ▼
Temp table + UPDATE … FROM (batch charge write-back)
        │
        ▼
Dapper CTEs → billing summary / call detail pages
```

---

## Prerequisites

| Tool           | Version     |
| -------------- | ----------- |
| .NET SDK       | 10.0+       |
| Docker Desktop | any recent  |

Optional (for local development without Docker):

- PostgreSQL 16
- Node.js 20+ (to run the frontend outside Docker)

---

## Quick Start

```bash
# Start the full stack (PostgreSQL + API + Web UI)
docker compose up --build
```

| Service | URL |
| ------- | --- |
| API     | `http://localhost:13001` |
| Web UI  | `http://localhost:5174` |
| Scalar interactive docs | `http://localhost:13001/scalar/v1` |
| OpenAPI JSON spec | `http://localhost:13001/openapi/v1.json` |

Database migrations are applied automatically on API startup. No manual migration step is needed.

### Running the API locally (without Docker)

```bash
# Start only the database
docker compose up postgres -d

# Run the API
dotnet run --project src/CdrBilling.Api
```

### Exporting the OpenAPI spec

```bash
curl http://localhost:13001/openapi/v1.json -o openapi.json
```

---

## Configuration

Default connection string in [src/CdrBilling.Api/appsettings.json](src/CdrBilling.Api/appsettings.json):

```
Host=localhost;Port=5432;Database=cdr_billing;Username=postgres;Password=postgres
```

Override via environment variable (takes precedence over `appsettings.json`):

```bash
ConnectionStrings__Postgres="Host=myhost;Port=5432;Database=cdr_billing;Username=postgres;Password=secret" \
  dotnet run --project src/CdrBilling.Api
```

The Docker Compose stack injects the connection string automatically via the `api` service environment block.

---

## Input File Formats

### CDR file — pipe-delimited `|`

```
StartTime|EndTime|CallingParty|CalledParty|CallDirection|Disposition|Duration|BillableSec|Charge|AccountCode|CallID|TrunkName
2026-02-03 14:22:10|2026-02-03 14:24:22|78123260000|+79161234567|outgoing|answered|132|127|0.45|Office_Billing|1234567890abcdef|SIP_Trunk_01
```

| Field | Type | Notes |
| ----- | ---- | ----- |
| `StartTime` / `EndTime` | `datetime` | Parsed as `DateTimeOffset` |
| `CallingParty` / `CalledParty` | `string` | Raw phone numbers, may include `+` prefix |
| `CallDirection` | enum | `incoming`, `outgoing`, `internal` |
| `Disposition` | enum | `answered`, `busy`, `no_answer`, `failed` |
| `Duration` | `int` | Total call duration in seconds |
| `BillableSec` | `int` | Seconds to bill |
| `Charge` | `decimal?` | Original charge from the source system (optional) |
| `AccountCode` | `string?` | Optional billing account |
| `CallID` | `string` | Unique call identifier |
| `TrunkName` | `string?` | Optional SIP trunk name |

The parser uses `System.IO.Pipelines` with a 64 KB read buffer. Malformed lines are silently skipped. Files up to 2 GB are supported.

### Tariff file — semicolon `;` CSV

```
prefix;destination;rate_per_min;connection_fee;timeband;weekday;priority;effective_date;expiry_date
7916;Moscow MTS (mobile);1.80;0.00;08:00-20:00;1-5;100;2026-01-01;2026-12-31
```

| Field | Type | Notes |
| ----- | ---- | ----- |
| `prefix` | `string` | Digit prefix for longest-match lookup |
| `destination` | `string` | Human-readable destination name |
| `rate_per_min` | `decimal` | Charge per billing minute |
| `connection_fee` | `decimal` | One-time connection fee per call |
| `timeband` | `string` | `HH:MM-HH:MM` or empty for all day; overnight bands (e.g. `20:00-08:00`) are supported |
| `weekday` | `string` | Range `1-5`, comma list `1,3,5`, or single digit `6` (1 = Monday, 7 = Sunday) |
| `priority` | `int` | Higher value wins when prefix length is equal |
| `effective_date` | `date` | Tariff validity start (`yyyy-MM-dd`) |
| `expiry_date` | `date?` | Tariff validity end (optional) |

### Subscriber file — semicolon `;` CSV

```
phone_number;client_name
78123264903;Ivan Ivanov
```

Subscribers define the set of phone numbers whose calls will appear in billing summaries.

---

## API Reference

### Sessions

| Method | Path | Description | Response |
| ------ | ---- | ----------- | -------- |
| `POST` | `/api/sessions` | Create a new billing session | `{ "sessionId": "..." }` |
| `GET` | `/api/sessions/{id}/status` | Session status and progress % | `SessionStatusDto` |

### File Upload

All endpoints accept `multipart/form-data` with a single field named `file`. Maximum upload size: **2 GB**.

| Method | Path | Description |
| ------ | ---- | ----------- |
| `POST` | `/api/sessions/{id}/upload/cdr` | Upload CDR file |
| `POST` | `/api/sessions/{id}/upload/tariff` | Upload tariff file |
| `POST` | `/api/sessions/{id}/upload/subscribers` | Upload subscriber file |

Each upload returns:

```json
{ "recordsImported": 12345, "message": "Imported 12345 CDR records." }
```

### Billing

| Method | Path | Description | Notes |
| ------ | ---- | ----------- | ----- |
| `POST` | `/api/sessions/{id}/run` | Start tariffication | Returns `202 Accepted` immediately |
| `GET` | `/api/sessions/{id}/progress` | SSE stream of progress events | `text/event-stream` |
| `GET` | `/api/sessions/{id}/results/summary` | Total charge per subscriber | Ordered by `totalCharge DESC` |
| `GET` | `/api/sessions/{id}/results/calls` | Paged call records with computed charges | See query params below |

**Query params for `/results/calls`:**

| Param | Type | Default | Description |
| ----- | ---- | ------- | ----------- |
| `phone` | `string` | — | Filter by calling or called party |
| `page` | `int` | `1` | Page number (1-based) |
| `pageSize` | `int` | `50` | Records per page (max 200) |

**SSE progress event format** (`event: progress`):

```json
{ "processed": 5000, "total": 12345, "percent": 40, "status": "Running" }
```

On completion or error:

```json
{ "processed": 12345, "total": 12345, "percent": 100, "status": "Completed" }
{ "processed": 3000,  "total": 12345, "percent": 24,  "status": "Failed", "error": "..." }
```

---

## Tariffication Rules

1. Only `outgoing` calls with `disposition = answered` are billed. `incoming` and `internal` calls are excluded from charges.
2. The number used for prefix lookup is `CalledParty` (called number) for outgoing calls.
3. Phone numbers are normalized to digits only before lookup (e.g. `+7916 123-4567` → `79161234567`).
4. The prefix trie returns all tariff lists whose stored prefix is a prefix of the normalized number. For example, if `79` and `7916` are both in the trie and the number is `79161234567`, both are candidates.
5. Candidates are filtered by:
   - `effective_date ≤ call_date ≤ expiry_date`
   - Call start time falls within `timeband` (overnight bands supported)
   - Call weekday matches `weekday` bitmask
6. The **best tariff** is chosen by: longest matching prefix first, then highest `priority` value.
7. **Charge formula**: `ConnectionFee + ⌈BillableSec / 60⌉ × RatePerMin`, rounded to 2 decimal places using `MidpointRounding.AwayFromZero`.

If no tariff matches, the call record is left with `computedCharge = null` (unrated).

---

## End-to-End Usage Example

```bash
# 1. Create session
SESSION_ID=$(curl -s -X POST http://localhost:13001/api/sessions | jq -r '.sessionId')
echo "Session: $SESSION_ID"

# 2. Upload files
curl -F "file=@cdr.txt"          http://localhost:13001/api/sessions/$SESSION_ID/upload/cdr
curl -F "file=@tariffs.csv"      http://localhost:13001/api/sessions/$SESSION_ID/upload/tariff
curl -F "file=@subscribers.csv"  http://localhost:13001/api/sessions/$SESSION_ID/upload/subscribers

# 3. Watch live progress in background, then start tariffication
curl -N http://localhost:13001/api/sessions/$SESSION_ID/progress &
curl -X POST http://localhost:13001/api/sessions/$SESSION_ID/run

# 4. View results
curl http://localhost:13001/api/sessions/$SESSION_ID/results/summary
curl "http://localhost:13001/api/sessions/$SESSION_ID/results/calls?page=1&pageSize=50"

# 5. Filter calls by phone number
curl "http://localhost:13001/api/sessions/$SESSION_ID/results/calls?phone=79161234567&page=1&pageSize=50"
```

---

## Project Structure

```
src/
  CdrBilling.Domain/
    Entities/             BillingSession, CallRecord, TariffEntry, Subscriber
    Enums/                CallDirection, Disposition, SessionStatus, DayOfWeekMask
    Services/             PrefixTrie<T>, TarificationEngine, TarificationCall
  CdrBilling.Application/
    Abstractions/         IBillingSessionRepository, ICallRecordRepository,
                          ITariffRepository, ISubscriberRepository, ISessionProgressReporter
    DTOs/                 Request/response records
    UseCases/             MediatR command/query handlers
    Options/              TarificationOptions
  CdrBilling.Infrastructure/
    Parsing/              CdrFileParser, TariffFileParser, SubscriberFileParser
    Persistence/          AppDbContext, entity configurations, EF Core repositories,
                          Npgsql binary COPY bulk operations, Dapper read queries
    Realtime/             SseProgressHub, SseProgressReporter
    Migrations/           EF Core auto-generated SQL migrations
  CdrBilling.Api/
    Endpoints/            SessionEndpoints, UploadEndpoints, BillingEndpoints, ProgressEndpoints
    Program.cs            DI composition root, Kestrel configuration
web/
  src/
    features/tariffication/   Upload flow, results panel, SSE progress consumer
    pages/                    HomePage, NewSessionPage, HistoryPage
    api.ts                    Typed API client
docker-compose.yml            PostgreSQL 16, API, Web UI services
```

---

## Performance Design

| Concern | Solution |
| ------- | -------- |
| Large CDR file ingestion | `System.IO.Pipelines` with 64 KB read buffer + Npgsql binary `COPY FROM STDIN` — avoids per-row round-trips |
| Tariff lookup | In-memory prefix trie — O(k) per call where k = number length |
| Batch charge write-back | Npgsql binary `COPY` into a temp table, then single `UPDATE … FROM` |
| Progress reporting | `Channel<ProgressEvent>` per session + `TypedResults.ServerSentEvents` (SSE) |
| Read queries | Dapper + raw SQL CTEs — no ORM overhead for billing summaries |
| Background processing | `IServiceScopeFactory` scoped `Task.Run` — HTTP 202 returned before processing starts |
| Upload size limit | Kestrel + `FormOptions` configured for 2 GB request bodies |

---

## Database Migrations

Migrations are applied automatically on every API startup via `db.Database.MigrateAsync()`.

To manage migrations manually (requires `dotnet-ef` global tool):

```bash
# Install the tool (if not already installed)
DOTNET_ROOT=/opt/homebrew/Cellar/dotnet/10.0.103/libexec \
  dotnet tool install --global dotnet-ef

# Add a new migration
DOTNET_ROOT=/opt/homebrew/Cellar/dotnet/10.0.103/libexec \
  ~/.dotnet/tools/dotnet-ef migrations add <MigrationName> \
  --project src/CdrBilling.Infrastructure \
  --startup-project src/CdrBilling.Api

# Apply pending migrations
DOTNET_ROOT=/opt/homebrew/Cellar/dotnet/10.0.103/libexec \
  ~/.dotnet/tools/dotnet-ef database update \
  --project src/CdrBilling.Infrastructure \
  --startup-project src/CdrBilling.Api
```
