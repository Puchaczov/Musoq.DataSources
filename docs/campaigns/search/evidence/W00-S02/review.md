# W00-S02 independent review

Verdict: approved

Reviewer: separate `grill-code` review pass

## Scope reviewed

- `scripts/search/Resolve-SearchEnvironment.ps1`
- `scripts/search/Test-SearchEnvironmentResolution.ps1`
- `docs/campaigns/search/evidence/W00-S02/environment-lock.json`
- `docs/campaigns/search/evidence/W00-S02/authority-map.json`

## Findings

No material correctness, ownership-boundary, resource-lifecycle or provenance issues were found.

The resolver rejects unresolved or relative configured paths, verifies the datasource origin and authority files, compares all five package pins, and keeps packaged-engine mode as the default. Source-engine mode requires an explicit checkout, repository identity and exact commit lock. Configured target executables are invoked by their literal paths, while missing CLI and ripgrep values are reported as blocked rather than discovered from PATH. The fixture harness exercises the required failure cases and a Unicode/space-containing checkout without changing the target repository.

## Residual boundaries

- The target CLI executable and CLI checkout are not configured in this host; the lock records both as blocked, so no CLI version or behavior is claimed.
- No source engine checkout was available in the actual local configuration; source-mode behavior is covered only by an isolated read-only fixture with an exact commit lock.
- PSScriptAnalyzer is not installed in this environment; PowerShell parsing and the isolated runtime harness passed.
- The full suite retains 31 existing expected skips and emits the existing NU1902/NU1903 package-audit warnings; this scope changed no package versions.

## Verification

- `pwsh -NoProfile -NonInteractive -File scripts/search/Test-SearchEnvironmentResolution.ps1`: exit 0; 5 scenarios; 16 assertions.
- `pwsh -NoProfile -NonInteractive -File scripts/search/Resolve-SearchEnvironment.ps1 -ConfigPath docs/campaigns/search/environment.local.json`: exit 0; packaged mode selected; package train matched; ripgrep 15.2.0 resolved; CLI prerequisites blocked explicitly.
- `dotnet test --configuration Release`: exit 0; 20 project summaries; 1,284 discovered, 1,253 passed, 0 failed, 31 skipped.
- `git diff --check`: pending final staged-scope check before commit.
