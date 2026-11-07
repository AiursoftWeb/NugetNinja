# Compatible npm updates

Ninja can update a single root npm package alongside its existing file conventions.
Add this to `ninja.yaml` to opt in:

```yaml
npm:
  updateDependencies: true
```

Run `ninja upgrade-pkg --path .` or the usual `ninja all --path .`.
`--dry-run` resolves and previews changes without changing the repository.
The existing PR bot uses the same upgrade plugin and contribution branch.

Node.js and npm must be on PATH. The package must have a stable three-part version
and a format 2 or 3 `package-lock.json`. Dependencies, dev dependencies and optional
dependencies may use exact stable versions, `^` or `~`. Direct dependency updates stay within the
current major version; `0.x` packages also stay within the current minor version.
Existing exact/caret/tilde declaration styles are preserved. Unsupported specs,
workspaces and shrinkwrap fail explicitly when opted in; other repositories are
unchanged by default.

Npm resolves the graph in a temporary directory using the project's `.npmrc`,
without lifecycle scripts, audits or package downloads. Peer conflicts and registry
failures leave the original files unchanged. Candidate versions are checked again
before creating a change. The manifest and lockfile are updated together; changed
dependency declarations or a changed lockfile increment the package's patch version once. A second unchanged run does
not increment it. Builds, tests, review and publishing remain the existing CI jobs.
Valid locked transitive versions are retained unless npm resolution needs to change them;
this is a direct-dependency updater, not a recursive vulnerability remediation command.

The generated lockfile uses npm-style JSON formatting. Exclude `package-lock.json`
from source-only formatter checks; keep validating it with `npm ci`.

The existing `dotnet test` suite exercises native npm against a local metadata-only
registry, including lockfile formats 2/3, compatible resolution, idempotence and
failure recovery. Node.js and npm must also be on PATH when running these tests.
