# Security Policy

## Supported versions

This project is developed on `main`. Security fixes land there, and there are no maintained
release branches. If you are running VisaBot, track `main`.

## Reporting a vulnerability

**Please do not open a public issue for a security problem.**

Use GitHub's private vulnerability reporting instead:
[**Report a vulnerability**](https://github.com/YusufSizmaz/visa-bot/security/advisories/new).
It creates a private thread visible only to the maintainers.

Helpful things to include:

- What an attacker can do, and what access they need to start
- Steps to reproduce, or a minimal proof of concept
- The commit or version you tested

We will confirm receipt and keep you updated as we work on a fix. If you would like credit in
the advisory, say so and we will include you.

## Scope

VisaBot is self-hosted, so the security boundary is the deployment you control. Reports about
this codebase are in scope, for example:

- Authentication bypass on the admin API
- Injection through scraped content, source configuration, or channel messages
- Secrets leaking into logs, API responses, or built images
- Flaws in the container or compose setup that expose internal services

Out of scope:

- Vulnerabilities in a deployment you do not operate, including `visa.codynlab.dev`
- Findings that require an attacker to already have the admin API key or server access
- Denial of service through traffic volume against a self-hosted instance

## Hardening notes for operators

If you deploy VisaBot yourself, a few things are worth knowing:

- **Never publish the API or database ports.** `compose.production.yaml` deliberately exposes
  only the admin container. The API trusts `X-Forwarded-For` unconditionally, because proxy
  addresses are not predictable on a container network — publishing its port would make the rate
  limiter bypassable.
- **Use a long, random `API_KEY`.** It is the only thing protecting the admin panel. The
  application refuses to start with fewer than 16 characters, but that is a floor, not a target.
  `openssl rand -base64 32` is a good source.
- **Keep secrets in your platform's environment configuration**, not in `appsettings.json`.
  The production compose file has no default values for secrets on purpose: a missing variable
  stops the deployment instead of quietly starting with a weak one.
- **Restrict the Telegram bot token to the channel it needs.** The bot only has to be an admin
  of the target channel; it needs no other privilege.
