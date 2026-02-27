# CdrBilling

High-performance ASP.NET Core 10 backend for processing telecom Call Detail Records (CDR), applying tariffs, and producing billing summaries.

---

## Prerequisites

| Tool           | Version    |
| -------------- | ---------- |
| .NET SDK       | 10.0+      |
| Docker Desktop | any recent |

---

## Quick Start

```bash
# 1. Start PostgreSQL
docker compose up -d

# 2. Run the API (migrations applied automatically on startup)
dotnet run --project src/CdrBilling.Api
```

The API is now running at `http://localhost:13000`.

---

## API Docs

| URL                                      | Description             |
| ---------------------------------------- | ----------------------- |
| `http://localhost:13000/scalar/v1`       | Interactive API UI      |
| `http://localhost:13000/openapi/v1.json` | Raw OpenAPI spec (JSON) |

To export the spec as a file:

```bash
curl http://localhost:13000/openapi/v1.json -o swagger.json
```

---

## Configuration

Default connection string in `src/CdrBilling.Api/appsettings.json`:

```
Host=localhost;Port=5432;Database=cdr_billing;Username=postgres;Password=postgres
```

Override via environment variable:

```bash
ConnectionStrings__Postgres="Host=..." dotnet run --project src/CdrBilling.Api
```

---

## Input File Formats

### CDR file — pipe-delimited `|`

```
StartTime|EndTime|CallingParty|CalledParty|CallDirection|Disposition|Duration|BillableSec|Charge|AccountCode|CallID|TrunkName
2026-02-03 14:22:10|2026-02-03 14:24:22|78123260000|+79161234567|outgoing|answered|132|127|0.45|Office_Billing|1234567890abcdef|SIP_Trunk_01
```

- `CallDirection`: `incoming`, `outgoing`, `internal`
- `Disposition`: `answered`, `busy`, `no_answer`, `failed`

### Tariff file — semicolon `;` CSV

```
prefix;destination;rate_per_min;connection_fee;timeband;weekday;priority;effective_date;expiry_date
7916;Москва МТС;1.80;0.00;08:00-20:00;1-5;100;2026-01-01;2026-12-31
```

- `timeband`: `HH:MM-HH:MM` (e.g. `08:00-20:00`) or empty for all day
- `weekday`: range `1-5`, list `1,3,5`, or single digit `6` (1=Mon, 7=Sun)

### Subscriber file — semicolon `;` CSV

```
phone_number;client_name
78123264903;Иванов Иван Иванович
```

---

## API Endpoints

### Sessions

| Method | Path                        | Description                   |
| ------ | --------------------------- | ----------------------------- |
| `POST` | `/api/sessions`             | Create a new billing session  |
| `GET`  | `/api/sessions/{id}/status` | Session status and progress % |

### Upload

| Method | Path                                    | Description                                                   |
| ------ | --------------------------------------- | ------------------------------------------------------------- |
| `POST` | `/api/sessions/{id}/upload/cdr`         | Upload CDR file (`multipart/form-data`, field: `file`)        |
| `POST` | `/api/sessions/{id}/upload/tariff`      | Upload tariff file (`multipart/form-data`, field: `file`)     |
| `POST` | `/api/sessions/{id}/upload/subscribers` | Upload subscriber file (`multipart/form-data`, field: `file`) |

### Billing

| Method | Path                                 | Description                                     |
| ------ | ------------------------------------ | ----------------------------------------------- |
| `POST` | `/api/sessions/{id}/run`             | Start tariffication — returns `202` immediately |
| `GET`  | `/api/sessions/{id}/progress`        | SSE stream — real-time progress events          |
| `GET`  | `/api/sessions/{id}/results/summary` | Total charge per subscriber                     |
| `GET`  | `/api/sessions/{id}/results/calls`   | Paged call records with computed charges        |

Query params for `/results/calls`: `?phone=79161234567&page=1&pageSize=50` (pageSize max 200)

---

## End-to-End Example

```bash
# 1. Create session
SESSION_ID=$(curl -s -X POST http://localhost:13000/api/sessions | jq -r '.sessionId')
echo "Session: $SESSION_ID"

# 2. Upload files
curl -F "file=@cdr.txt"         http://localhost:13000/api/sessions/$SESSION_ID/upload/cdr
curl -F "file=@tariffs.csv"     http://localhost:13000/api/sessions/$SESSION_ID/upload/tariff
curl -F "file=@subscribers.csv" http://localhost:13000/api/sessions/$SESSION_ID/upload/subscribers

# 3. Watch progress (background) and start tariffication
curl -N http://localhost:13000/api/sessions/$SESSION_ID/progress &
curl -X POST http://localhost:13000/api/sessions/$SESSION_ID/run

# 4. View results
curl http://localhost:13000/api/sessions/$SESSION_ID/results/summary
curl "http://localhost:13000/api/sessions/$SESSION_ID/results/calls?page=1&pageSize=50"
```

---

## Tariffication Rules

1. Only `answered` calls are billed; `internal` calls are always skipped.
2. The number used for prefix lookup: `CalledParty` for outgoing calls, `CallingParty` for incoming.
3. Phone numbers are normalized to digits only before lookup.
4. Best tariff selected by: longest matching prefix → highest priority.
5. Tariff must satisfy: `effective_date ≤ call_date ≤ expiry_date`, timeband, and weekday mask.
6. Charge formula: `ConnectionFee + (BillableSec / 60) × RatePerMin`, rounded to 4 decimal places.

---

## Migrations

Migrations run automatically on startup. To manage them manually:

```bash
# Add a new migration
DOTNET_ROOT=/opt/homebrew/Cellar/dotnet/10.0.103/libexec \
  ~/.dotnet/tools/dotnet-ef migrations add <MigrationName> \
  --project src/CdrBilling.Infrastructure \
  --startup-project src/CdrBilling.Api

# Apply migrations
DOTNET_ROOT=/opt/homebrew/Cellar/dotnet/10.0.103/libexec \
  ~/.dotnet/tools/dotnet-ef database update \
  --project src/CdrBilling.Infrastructure \
  --startup-project src/CdrBilling.Api
```

---

## Architecture

```
CdrBilling.Domain         — Entities, enums, prefix trie, tariffication engine
CdrBilling.Application    — MediatR use cases, repository interfaces, DTOs
CdrBilling.Infrastructure — EF Core + Dapper + Npgsql binary COPY + SSE hub
CdrBilling.Api            — Minimal API endpoints, DI composition root
```

Key performance decisions:

| Concern               | Solution                                       |
| --------------------- | ---------------------------------------------- |
| Large CDR ingestion   | `System.IO.Pipelines` + Npgsql binary `COPY`   |
| Tariff lookup         | In-memory prefix trie — O(k) per call          |
| Batch DB updates      | Temp table + `UPDATE … FROM`                   |
| Progress reporting    | `Channel<ProgressEvent>` per session + SSE     |
| Read queries          | Dapper + raw SQL for billing summaries         |
| Background processing | `IServiceScopeFactory` + `Task.Run` → HTTP 202 |
