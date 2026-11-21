# API Aggregation Service

An ASP.NET Core (.NET 9) **minimal API** that fetches data from several external APIs **in parallel**, normalises it into one model and exposes it through a single endpoint with filtering, sorting and paging. It also provides request statistics per external API, logs performance anomalies in the background and is secured with JWT bearer authentication.

| External API | Category | API key | What is aggregated |
|---|---|---|---|
| [Open-Meteo](https://open-meteo.com) | Weather | none | Daily forecast for the city (geocoded first) |
| [Hacker News (Algolia)](https://hn.algolia.com/api) | News | none | Stories matching the topic |
| [GitHub Search](https://docs.github.com/en/rest/search) | Repository | optional | Most starred repositories matching the topic |

> None of the APIs needs a key, so the service works out of the box. GitHub accepts an optional personal access token (`ExternalApis:GitHub:ApiKey`) for a higher rate limit.

---

## Quick start

Prerequisites: [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0).

```bash
dotnet run --project API_Aggregation.Api      # http://localhost:5281
dotnet test                                   # 102 unit tests
```

* OpenAPI document: <http://localhost:5281/openapi/v1.json> (Development only; can be imported into Postman)
* Ready-made requests: [`API_Aggregation.Api/API_Aggregation.http`](API_Aggregation.Api/API_Aggregation.http) (VS / VS Code / Rider)

### Run with Docker Compose (API + Jaeger tracing)

No .NET SDK needed; from the repository root:

```bash
docker compose up --build
```

| | URL |
|---|---|
| API | <http://localhost:8080> (e.g. `POST /api/v1/auth/token`, `GET /api/v1/aggregation?city=Athens`) |
| Jaeger UI | <http://localhost:16686>, service `API_Aggregation-api` |

In Jaeger, each `GET /api/v1/aggregation` request appears as one trace, with the calls to the external APIs as child spans running **in parallel**. The compose file sets `OpenTelemetry__OtlpEndpoint=http://jaeger:4317` (inside the Docker network Jaeger is reached by its service name) and runs the API in the `Development` environment so `/openapi/v1.json` is available. Stop everything with `docker compose down`.

To use the `.http` file against the container, change `@HostAddress` to `http://localhost:8080`.

To see traces while running the API with `dotnet run`, start only Jaeger: `docker compose up jaeger` (the API's default `OtlpEndpoint` is `http://localhost:4317`).

> **Note:** antivirus software that inspects HTTPS traffic (e.g. Kaspersky) re-signs certificates with its own root, which Windows trusts but the Linux container does not. Affected external APIs then fail inside Docker with `UntrustedRoot` and are reported as `Unavailable` (the fallback keeps the rest of the response working). Running with `dotnet run`, or excluding the site from HTTPS scanning, avoids it.

---

## Endpoints

All business endpoints are versioned (`/api/v1/...`) and, except for the token endpoint, require `Authorization: Bearer <token>`. A missing, invalid or expired token returns `401 Unauthorized`.

Request and response bodies are JSON with camelCase property names; dates are ISO 8601 (`2026-11-21T09:46:34+00:00`).

### `POST /api/v1/auth/token` – obtain a JWT

```json
{ "username": "demo", "password": "demo-password" }
```

`200 OK`

```json
{ "accessToken": "eyJhbGciOi...", "expiresAt": "2026-11-21T10:16:34+00:00", "tokenType": "Bearer" }
```

`401` for invalid credentials, `400` when username / password are missing.

### `GET /api/v1/aggregation` – aggregated data

| Query parameter | Default | Description |
|---|---|---|
| `city` | **required** | Location for the weather APIs, e.g. `Athens` |
| `topic` | = `city` | Search term for the news / repository APIs |
| `category` | all | `Weather`, `News` or `Repository` – only APIs of this category are **called** |
| `sources` | all | Comma separated API names (`OpenMeteo,HackerNews`) – only these APIs are **called** |
| `from`, `to` | – | Inclusive date range on the item timestamp (ISO 8601) |
| `search` | – | Text that must appear in the title or description (case-insensitive) |
| `sortBy` | `date` | `date`, `relevance`, `title`, `source` |
| `sortOrder` | `desc` | `asc` or `desc` |
| `page` / `pageSize` | `1` / `20` | Paging, `pageSize` 1–100 |

Example: `GET /api/v1/aggregation?city=Athens&topic=dotnet&sortBy=relevance&pageSize=2`

```json
{
  "city": "Athens",
  "topic": "dotnet",
  "generatedAt": "2026-11-21T09:46:34.48+00:00",
  "totalItems": 47,
  "page": 1,
  "pageSize": 2,
  "totalPages": 24,
  "sources": [
    { "source": "OpenMeteo",      "status": "Live",     "itemCount": 7,  "responseTimeMs": 663.35, "error": null },
    { "source": "HackerNews",     "status": "Cached",   "itemCount": 20, "responseTimeMs": 0,      "error": null },
    { "source": "GitHub",         "status": "Stale",    "itemCount": 20, "responseTimeMs": 15002,  "error": "The external API is currently unavailable. Showing the last known data." }
  ],
  "items": [
    {
      "source": "OpenMeteo",
      "category": "Weather",
      "title": "Athens: 10.7–15.2°C, overcast",
      "description": "Daily forecast for 2026-11-21. Precipitation 0 mm, max wind 8 km/h.",
      "timestamp": "2026-11-21T00:00:00+02:00",
      "relevance": 0.9,
      "url": "https://open-meteo.com/en/docs#latitude=37.98&longitude=23.72",
      "metadata": { "temperatureMinC": "10.7", "temperatureMaxC": "15.2", "condition": "overcast", "country": "Greece" }
    }
  ]
}
```

**Source status values**

| Status | Meaning |
|---|---|
| `Live` | Fetched from the external API during this request |
| `Cached` | Served from the cache – no external call |
| `Stale` | The API failed; the last known good data is returned (fallback) |
| `Unavailable` | The API failed and there is no fallback data – the rest of the response is still returned |
| `Disabled` | Disabled in configuration (`ExternalApis:<Name>:Enabled = false`) |

**Relevance** is normalised to `[0, 1]` per source: weather → proximity of the forecast to *now*; news → the provider's own ranking position; repositories → stars relative to the most starred result.

Validation errors return `400` with a problem-details body:

```json
{
  "title": "One or more validation errors occurred.",
  "status": 400,
  "detail": "Validation failed: 'city' is required., 'sortBy' must be one of: Date, Relevance, Title, Source.",
  "errors": { "City": ["'city' is required."], "SortBy": ["'sortBy' must be one of: Date, Relevance, Title, Source."] }
}
```

### `GET /api/v1/statistics` – request statistics

For every registered API: total requests, failed requests, overall average response time and the requests grouped in performance buckets. Only **real external calls** are counted (cache hits are not). APIs that have not been called yet are listed with zeros.

```json
{
  "generatedAt": "2026-11-21T09:46:34.60+00:00",
  "fastThresholdMs": 100,
  "slowThresholdMs": 200,
  "apis": [
    {
      "apiName": "HackerNews",
      "totalRequests": 12,
      "failedRequests": 1,
      "averageResponseTimeMs": 231.4,
      "buckets": [
        { "bucket": "Fast",    "lowerBoundMs": null, "upperBoundMs": 100,  "requestCount": 3, "averageResponseTimeMs": 72.1 },
        { "bucket": "Average", "lowerBoundMs": 100,  "upperBoundMs": 200,  "requestCount": 4, "averageResponseTimeMs": 151.0 },
        { "bucket": "Slow",    "lowerBoundMs": 200,  "upperBoundMs": null, "requestCount": 5, "averageResponseTimeMs": 393.6 }
      ]
    }
  ]
}
```

Buckets: **Fast** `< FastThresholdMs`, **Average** `FastThresholdMs – SlowThresholdMs` (inclusive), **Slow** `> SlowThresholdMs`.

### `GET /health` – liveness probe (anonymous)

---

## Architecture

Clean-architecture layering with CQRS (MediatR) and minimal API endpoints:

```
API_Aggregation.Api             Program.cs: minimal API endpoints (thin: bind → ISender.Send → result), versioning, OpenAPI
        │
API_Aggregation.Infrastructure  External API clients (typed HttpClient + resilience), in-memory statistics store,
        │                       anomaly background service, JWT token service, DI wiring
API_Aggregation.Application     CQRS queries/commands + handlers + FluentValidation validators, validation pipeline,
        │                       ExternalApiFetcher (cache / fallback / stats), filtering & sorting, Mapperly mapper
API_Aggregation.Domain          AggregatedItem, SourceResult/SourceStatus, statistics models
```

| CQRS request | Type | Endpoint |
|---|---|---|
| `GetAggregatedDataQuery` | Query | `GET /api/v1/aggregation` |
| `GetApiStatisticsQuery` | Query | `GET /api/v1/statistics` |
| `GenerateTokenCommand` | Command | `POST /api/v1/auth/token` |

### Request flow – `GET /aggregation`

1. The endpoint in `Program.cs` binds the query string straight into a `GetAggregatedDataQuery` (`[AsParameters]`) and sends it through MediatR.
2. `ValidationBehavior` runs the FluentValidation validator → `400` on failure.
3. The handler **selects the clients** that match `category` / `sources` (APIs whose data would be filtered out are never called).
4. All selected clients run **in parallel** (`Task.WhenAll`) through `ExternalApiFetcher`:
   * **fresh cache hit** → `Cached` (no external call);
   * otherwise the call goes through the **resilience pipeline** (see below), is **measured** and recorded in the statistics store, and its result is cached;
   * on failure → last known good data from the **stale cache** (`Stale`) or an empty `Unavailable` result. The fetcher never throws for an API failure, so one broken API never breaks the response.
5. Items from all sources are filtered (date range, text), sorted, paged and mapped to DTOs with **Mapperly**.

### Adding a new external API

1. Create a class deriving from `ExternalApiClientBase` (or implementing `IExternalApiClient`) that converts the provider's payload into `AggregatedItem`s.
2. Register it with one line in `ExternalApisConfiguration.AddExternalApis`:
   `services.AddExternalApiClient<MyClient>(MyClient.ApiName, configuration);`
3. Add an `ExternalApis:<Name>` section to `appsettings.json`.

Caching, resilience, fallback, statistics, filtering, sorting, the statistics endpoint and anomaly detection then apply automatically.

---

## Cross-cutting concerns

### Error handling, transient failures and fallback

* **Resilience pipeline** on every API client (`Microsoft.Extensions.Http.Resilience` / Polly, standard handler): total timeout (15 s) → **retry** up to 2 times with exponential back-off + jitter (only transient errors: 5xx, 408, 429, network errors) → **circuit breaker** (stops hammering an API that is down) → per-attempt timeout (5 s). The same settings are used for all APIs (constants in `ExternalApisConfiguration`).
* **Fallback**: every successful response is also stored in a long-lived *stale* cache entry; it is served when the API fails. If nothing is available the API is reported as `Unavailable` and the other APIs' data is still returned.
* A `404` from a provider (e.g. unknown city) is treated as "no data", not as an outage.
* Exception details are logged but never returned to the caller. Unhandled exceptions are returned as RFC 7807 problem details.

### Caching

* `IMemoryCache` per API and per *relevant* parameters: weather clients key on the city only and news/repository clients on the topic only, so changing one parameter doesn't invalidate the others' cache.
* *Possible improvement for high traffic:* identical requests arriving while the first call is still running each call the API until the result is cached. Request de-duplication (letting them share the running call) would prevent such bursts; it was left out to keep the fetcher simple.
* Durations are configurable per API (`CacheDurationSeconds`, `StaleCacheDurationMinutes`).

### Parallelism and async

* All external APIs are called concurrently; the response time is roughly that of the *slowest* API instead of the sum of all.
* Fully async I/O end to end, with `CancellationToken` propagated from the HTTP request down to the external calls: if the caller disconnects, the pending external requests are cancelled too.
* `IHttpClientFactory` typed clients (pooled handlers, no socket exhaustion).

### Statistics and thread safety

`InMemoryApiStatisticsStore` uses a `ConcurrentDictionary` for lock-free lookup of the per-API accumulator and a **per-API lock** around the few arithmetic updates. This guarantees consistent snapshots (counts, totals and buckets of one API always agree), and requests to different APIs never contend. Individual samples are kept, bounded by `SampleRetentionMinutes` and `MaxSamplesPerApi`, to compute recent-window averages.

### Performance anomaly detection (background service)

`PerformanceAnomalyBackgroundService` runs every `IntervalSeconds` and logs a **warning** for every API whose average over the last `WindowMinutes` (default 5) is more than `ThresholdPercent` (default 50 %) above its overall average, provided the window contains at least `MinimumSamples` requests:

```
Performance anomaly detected for GitHub: average over the last 5 min is 812.4 ms (6 requests) vs overall average 301.7 ms (+169.3%)
```

### Security

* JWT bearer authentication (HMAC-SHA256) with issuer, audience, lifetime and signing-key validation. The key must be at least 256 bits (checked at startup).
* Demo users come from configuration (`Auth:Users`) and are compared in constant time. **For production**, replace them with an identity provider, and supply `Jwt:Key` / API keys through secrets or environment variables, not `appsettings.json`.

---

## Configuration (`appsettings.json`)

| Section | Key settings |
|---|---|
| `ExternalApis:<Name>` | `Enabled`, `BaseUrl`, `ApiKey`, `MaxItems`, `CacheDurationSeconds`, `StaleCacheDurationMinutes`, `Settings` (API specific, e.g. `GeocodingUrl`) |
| `Statistics` | `FastThresholdMs` (100), `SlowThresholdMs` (200), `SampleRetentionMinutes`, `MaxSamplesPerApi` |
| `AnomalyDetection` | `Enabled`, `IntervalSeconds` (60), `WindowMinutes` (5), `ThresholdPercent` (50), `MinimumSamples` (3) |
| `Jwt` | `Key`, `Issuer`, `Audience`, `ExpiryMinutes` |
| `Auth:Users` | Demo credentials (`demo` / `demo-password`) |
| `OpenTelemetry:OtlpEndpoint` | OTLP/gRPC endpoint of Jaeger (or any OTLP collector). Default `http://localhost:4317`; in Docker Compose use the service name, e.g. `http://jaeger:4317`. Set to empty to disable exporting. |
| `Kestrel:Endpoints:Http:Url` | Listening address, `http://localhost:5281` (the Docker image overrides it to `http://+:8080`) |
| `Cors:AllowedOrigins` | Origins allowed to call the API from a browser |
| `Serilog` | Logging (console, compact JSON) |

Any value can be overridden with environment variables, e.g. `ExternalApis__GitHub__ApiKey=...`.

An API is only used when its `ExternalApis:<Name>` section exists: remove (or omit) the section and the client is not registered at all (it won't be called and won't appear in `/statistics`). The sections that are present are validated at startup (`ExternalApiOptionsValidator` + `ValidateOnStart`): a missing or invalid `BaseUrl` or out-of-range values stop the application with a clear message, e.g. `'ExternalApis:GitHub:BaseUrl' is required.`. To switch an API off temporarily while keeping its settings, use `Enabled: false` (it is then reported as `Disabled`).

---

## Tests

`API_Aggregation.UnitTests` (xUnit v3, Moq, `FakeTimeProvider`, `FakeLogger`): 102 tests, no network access needed.

| Area | What is covered |
|---|---|
| `ExternalApiFetcher` | live / cached / cache expiry, stale fallback, unavailable, disabled by configuration, statistics recorded only for real calls, cancellation of the external call, error details not leaked |
| Query handler | aggregation of all sources, **parallel execution**, category / source selection, topic default, partial failure, sorting & paging |
| Validation | validators and the MediatR validation pipeline |
| Filtering / sorting | every sort field and direction, date range, text search, paging edge cases |
| Statistics store | totals, averages, bucket boundaries, **thread safety under 16 concurrent writers + readers**, retention and sample cap |
| Anomaly detection | detection threshold, stable APIs, minimum samples, the background service logging |
| API clients | payload mapping for all three APIs via a stubbed `HttpMessageHandler`, 404 handling, server errors |
| Auth | token creation and validation, wrong key rejected, credential validation |
| Mapping | Mapperly mappings (enums as names, item count, buckets) |
| Configuration | `ExternalApiOptionsValidator` rules, clients skipped when their config section is missing, invalid sections rejected |
