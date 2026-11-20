# SOPs Packages CI/CD

Copy `.github/` and `eng/` into the root of `D:\SOPs-system\sops-packages`.

## Workflows

- **CI:** validates every discovered publishable project and its matching test project on pushes and pull requests.
- **Security:** weekly and on push/PR, restores with NuGet vulnerability auditing and builds the solution.
- **Release:** automatically triggered on `main` when CI completes successfully (or manually triggered via `workflow_dispatch`), computes independent versions, builds/tests, packs and publishes each eligible package to private GitHub Packages, then tags the published commit.

## Discovery

Every publishable `src/**/*.csproj` must have a matching `tests/<ProjectName>.Tests/<ProjectName>.Tests.csproj`. A project with `<IsPackable>false</IsPackable>` is skipped. Add new projects to `SOPs.Packages.sln` for full solution validation.

## Versioning

Package tags: `Sops.Configuration/v0.1.0`, `Sops.Logging/v0.1.0`, etc. Untagged packages begin at `0.1.0`. After a tag, a changed package receives PATCH by default, MINOR for `feat:` commits in its own source/test paths, and MAJOR for `!:` or `BREAKING CHANGE:`. Changes to shared configuration trigger all packages, but default to PATCH. Unchanged packages are not published. Existing tags are never overwritten.

**Limitations:** Version calculation uses changed files and Git commit messages, not a full dependency graph; cross-package dependencies and release ordering are not automatically resolved. `--skip-duplicate` is intended for safe reruns, but a package can be uploaded before its tag is pushed. The workflow uses GitHub-hosted Windows runners and assumes SDK 11 and .NET 8 runtime are available via `setup-dotnet`. Test real package consumption and GitHub permissions before relying on releases. For a production release process, add protected environments, package provenance, branch protection, and stronger cross-package dependency handling.

## GitHub setup

1. Create a **private** `SOPs-system/sops-packages` repository and push these files.
2. Set Actions workflow permissions so the `GITHUB_TOKEN` can write contents and packages; organization policy may constrain permissions.
3. Give the repository access to any existing private NuGet dependencies in the organization.
4. `NuGet.Config` must define or inherit sources named `nuget.org` and `github-sops` and map `Sops.*` to `github-sops`.
5. To release, use Actions → **Release private NuGet packages** → Run workflow on `main`.

No personal access token is embedded in these files.
