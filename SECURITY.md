# Security Policy

> Magyar verzió: [SECURITY-hu.md](SECURITY-hu.md)

Symphact is a capability-based actor runtime whose core value proposition is isolation and
unforgeable references. We take security reports seriously — including issues in the runtime,
the persistence layer, the scheduler, and the supervision model.

## Supported versions

Symphact is at **v0.5 (pre-alpha)**. Security fixes land on the `main` branch. There are no
released or back-ported versions yet; until a stable release exists, only `main` is supported.

| Version | Supported |
|---------|-----------|
| `main` (0.5.x pre-alpha) | ✅ |
| older commits | ❌ |

## Reporting a vulnerability

**Please do not open a public issue for security vulnerabilities.**

Report privately via GitHub:

1. Open the repository's **Security** tab → **Report a vulnerability**
   (<https://github.com/FenySoft/Symphact/security/advisories/new>).
2. Describe the issue, the affected component, and a reproduction (ideally a failing test).

If GitHub private reporting is unavailable to you, contact the maintainer privately through their
GitHub profile: [@Hocza-Jozsef-Szabolcs](https://github.com/Hocza-Jozsef-Szabolcs).

## What to include

- Affected component (`Symphact.Core`, `Symphact.Persistence`, scheduler, supervision, …)
- Symphact commit / version and .NET SDK version
- A minimal reproduction — a red xUnit test is ideal (the project is strictly test-first)
- Impact assessment: which isolation or capability invariant is broken?

## Our commitment

- We aim to acknowledge a report within **5 working days**.
- We will keep you informed about the fix and coordinate a disclosure timeline.
- With your permission, we will credit you in the advisory and release notes.

## Scope note

Symphact is co-designed with the [CFPU hardware](https://github.com/FenySoft/CLI-CPU); some
isolation properties are intended to become hardware-enforced in the future. At v0.5 the runtime
provides **software-enforced** isolation only — please frame reports against the current software
threat model (see [`docs/trust-model-en.md`](docs/trust-model-en.md)).
