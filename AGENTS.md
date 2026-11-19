# SOPs Packages - Agent Instructions

## Scope
This repository contains four independent private C# NuGet packages: Sops.Configuration, Sops.Logging, Sops.WhatsApp, and Sops.Payments.

## Skills
Before starting work, read the applicable using-agent-skills SKILL.md and discover relevant skills installed in the SOPs-system parent workspace. Read applicable skills before planning, coding, testing, reviewing, or publishing. If parent-level skills are not discoverable, report the limitation rather than assuming they loaded.

## Architecture
- Keep each NuGet package independently versioned and publishable.
- Do not introduce circular dependencies or unnecessary project layers.
- Use Microsoft.Extensions abstractions where appropriate.
- Preserve netstandard2.0 compatibility unless a change is explicitly approved.
- Keep public APIs minimal, documented, and backward-compatible.
- Use constructor dependency injection and explicit configuration.

## Security
- Never hardcode credentials, API keys, tokens, or secrets.
- Never log sensitive customer data, OTPs, payment credentials, or authorization headers.
- Verify external webhook signatures before trusting payloads.
- Use secure HTTP communication and appropriate timeouts.

## Development
- Preserve existing code, history, and uncommitted changes.
- Prefer small, incremental, testable changes.
- Do not add dependencies without explaining their purpose.
- Do not modify other repositories or SOPs-system/sops-backup.
- Run relevant builds, tests, formatting checks, and security checks.
- Report validation results and remaining limitations.

## Git and Publishing
- Never push, publish, tag, or create releases without explicit authorization.
- Use independent semantic versions for each package.
- Keep GitHub Packages private and use least-privilege credentials.
