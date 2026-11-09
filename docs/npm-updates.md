# Compatible npm updates

Ninja can update a root npm package and independent nested frontend packages, such as
`src/MyApp/wwwroot`, alongside its existing file conventions.
Add this to `ninja.yaml` to opt in:

```yaml
npm:
  updateDependencies: true
```

Run `ninja upgrade-pkg --path .` or the usual `ninja all --path .`.
`--dry-run` resolves and previews changes without changing the repository.
The existing PR bot uses the same upgrade plugin and contribution branch.
The normal `all` workflow synchronizes expected files before analyzing dependencies,
so an updated shared npm switch takes effect in that run. `upgrade-pkg` and the bot's
update-only mode use the repository's current configuration without synchronizing files.

Node.js and npm must be on PATH. Root publishing packages must have a stable
three-part version; nested frontend packages may omit it. Each package needs a
format 2 or 3 `package-lock.json`. Dependencies, dev dependencies and optional
dependencies may use exact stable versions, `^` or `~`. Direct dependency updates stay within the
current major version; `0.x` packages also stay within the current minor version.
Existing exact/caret/tilde declaration styles are preserved. Unsupported specs,
workspaces and shrinkwrap fail explicitly when opted in; other repositories are
unchanged by default.

Ninja finds manifest/lockfile pairs throughout the repository, skipping `.git`,
`node_modules`, `bin`, `obj` and directory links. A missing partner or unsupported
package fails with its path; it is not silently skipped. Repositories with no npm
manifest, lockfile or shrinkwrap are unchanged, so a shared opt-in is safe for .NET-only repositories. Analysis completes for all
packages before any npm changes are applied.

Npm resolves each graph in its own temporary directory using the package's `.npmrc`
(or the repository root `.npmrc` if the nested package has none),
without lifecycle scripts, audits or package downloads. Peer conflicts and registry
failures leave the original files unchanged. Candidate versions are checked again
before creating a change. The manifest and lockfile are updated together; changed
dependency declarations or a changed lockfile increment a root package's patch version once.
Nested frontend package versions stay unchanged: they are assets of their owning .NET
project, not independent npm releases. The owning project's existing version propagation
applies when running `all`. A second unchanged run does not increment versions. Builds, tests, review and publishing remain the existing CI jobs.
Valid locked transitive versions are retained unless npm resolution needs to change them;
this is a direct-dependency updater, not a recursive vulnerability remediation command.

For packages in separate repositories, the dependency must first be published and
visible in the configured registry. A subsequent bot run updates the consumer's
declaration and lockfile, including any newly required transitive versions, and
increments the consumer's patch version once. This is eventual propagation through
normal PRs and CI, not a simultaneous release of every dependent repository.
Stale registry metadata must never cause a downgrade: if no candidate satisfies the
installed version floor, resolution fails without changing the consumer's files.

The generated lockfile uses npm-style JSON formatting. Exclude `package-lock.json`
from source-only formatter checks; keep validating it with `npm ci`.

The existing `dotnet test` suite exercises native npm against a local metadata-only
registry, including lockfile formats 2/3, compatible resolution, idempotence and
failure recovery. Node.js and npm must also be on PATH when running these tests.
