# Contributing to VisaBot

Thanks for taking the time. Issues and pull requests are both welcome, and you do not need to
ask permission before opening either.

## Before you start

For anything larger than a bug fix, open an issue first so we can agree on the approach. It is
cheaper to disagree about a design in an issue than in a finished branch.

## Development setup

```bash
git clone https://github.com/YusufSizmaz/visa-bot.git
cd visa-bot

cp .env.example .env
docker compose up -d postgres redis

dotnet run --project src/VisaTelegramBot.WebApi
dotnet run --project src/VisaTelegramBot.Worker
npm --prefix web/admin ci && npm --prefix web/admin run dev
```

You need the .NET 9 SDK, Node.js 22+ and Docker. The API applies migrations and seeds a starter
set of sources on startup, so the panel has something to show immediately.

## Before you open a pull request

```bash
dotnet test
npm --prefix web/admin run typecheck
```

Both must pass. CI runs the same commands plus a production image build, so a green local run is
a good predictor.

## What we care about in review

**Keep the layers intact.** Dependencies point inward only: Domain knows nothing, Application
knows Domain, Infrastructure implements the ports Application defines, and the hosts wire it all
together. `ArchitectureTests` enforces this — if you add an EF Core reference to Domain or a
Telegram type to Application, the build fails. That is the intended behaviour, not an obstacle
to work around.

**Comments explain why, not what.** The codebase is also a teaching artifact. `// increments the
counter` adds nothing; `// sendMessage is not idempotent, so a blind retry double-posts` is the
kind of comment we want. Existing comments are in Turkish — match the file you are editing.

**Database changes need a migration.** After changing an entity or a configuration:

```bash
dotnet ef migrations add DescriptiveName \
  --project src/VisaTelegramBot.Infrastructure \
  --startup-project src/VisaTelegramBot.Infrastructure \
  --output-dir Persistence/Migrations
```

Commit the migration and the updated model snapshot together.

**Integration tests run against real PostgreSQL.** Locks, unique indexes and SQL translation
cannot be verified against an in-memory provider, so please do not replace them with one.
Testcontainers starts a throwaway database when Docker is running; set `VISABOT_TEST_POSTGRES`
to use your own server instead.

## Adding a news source

This usually needs no code at all. Add it from the admin panel, or add an entry to
`src/VisaTelegramBot.WebApi/Seed/news-sources.json` if it should ship with the project:

- `type: "Rss"` for RSS and Atom feeds — nothing else is required
- `type: "Html"` for sites without a feed, plus `parsingRules` with CSS selectors for the item,
  title, link, summary and published date
- `keywordSet` filters entries by keyword; leave it out to accept everything from that source
- `category` is `Visa` or `FlightCampaign` and only changes how the Telegram message is formatted

Seeding is skip-on-existing: a source already in the database is never overwritten by the seed
file, so edits made in the panel survive deployments.

## Commit messages

Write a subject line that says what changed and a body that says why, when the why is not
obvious. No strict format is enforced.

## Reporting bugs

Use the issue template and include what you expected, what happened, and the relevant log lines.
For anything security-related, see [SECURITY.md](SECURITY.md) instead — please do not open a
public issue.

## License

By contributing, you agree that your contributions are licensed under the
[MIT License](LICENSE) that covers this project.
