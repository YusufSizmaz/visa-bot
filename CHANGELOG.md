# Changelog

All notable changes to this project are documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project
adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.0.0] - 2026-09-21

First public release. The project is now open source under the MIT license.

### Added

- **Collection pipeline** — RSS and Atom feeds plus CSS-selector based HTML scraping, with a
  per-source polling interval, keyword filter and Turkish-only language filter. Each host gets
  its own circuit breaker, so one dead site never stalls the healthy ones.
- **Delivery pipeline** — outbox-style queue with exponential backoff and a dead-letter state,
  Telegram rate limiting, and a content-hash unique index as the last defence against duplicates.
  The first crawl of a new source is archived rather than published, so adding a source never
  floods the channel with months of backlog.
- **Admin panel** — React 19 SPA for sources, news items and manual channel messages with photo
  upload and scheduling. Every archived item shows the reason it was held back.
- **Web API** — API-key authentication with constant-time comparison, per-client rate limiting,
  problem-details error responses and OpenAPI documentation in development.
- **Deployment** — `compose.production.yaml` for Coolify: only the admin container is exposed,
  the database, cache and API stay on the internal network, and missing secrets stop the
  deployment instead of falling back to weak defaults. See [DEPLOY.md](DEPLOY.md).
- **CI** — GitHub Actions runs the full test suite, the admin panel typecheck and build, and a
  production image build on every push and pull request.
- Bilingual documentation ([English](README.md), [Türkçe](README.tr.md)), contribution guide,
  security policy, issue and pull request templates, and Dependabot configuration.

### Changed

- **Migrated from SQL Server to PostgreSQL 17.** SQL Server's Developer edition is not licensed
  for production use, so every provider-specific pattern was replaced with its PostgreSQL
  counterpart: `sp_getapplock` became `pg_try_advisory_lock`, the `UPDLOCK`/`READPAST` queue
  query became `FOR UPDATE SKIP LOCKED` with `UPDATE … RETURNING`, the `rowversion` shadow column
  became the `xmin` system column, and clustered indexes were dropped in favour of unique indexes
  that preserve keyset pagination order.

### Removed

- **Flight deal tracking via the Travelpayouts API.** The integration required an API token that
  is only issued to live sites, so the whole feature — domain model, price provider, route
  scheduler, REST endpoints and admin page — was removed rather than left as dead code.
  Flight campaigns still reach the channel: they come from `FlightCampaign` news sources
  (ucuzaucak.net, Pegasus, Enuygun, Sonfiyat) through the ordinary scraping pipeline.

### Fixed

- Integration tests connected to the test container's default database and then tried to drop it,
  failing seven tests. They now use a dedicated per-run database.
- The API container healthcheck called `wget`, which does not exist in the `aspnet:9.0` image.
  The check could never pass, so the worker and admin containers would never have started.
- The admin healthcheck resolved `localhost` to IPv6 while nginx listened on IPv4 only.
- API requests behind the reverse proxy were all attributed to the proxy's address, making the
  rate limiter ineffective and request logs useless. The API now honours `X-Forwarded-For`.

[Unreleased]: https://github.com/YusufSizmaz/visa-bot/compare/v1.0.0...HEAD
[1.0.0]: https://github.com/YusufSizmaz/visa-bot/releases/tag/v1.0.0
