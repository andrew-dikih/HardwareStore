## Summary

<!-- What does this PR change and why? -->

## Checklist

- [ ] Tests added/updated where applicable
- [ ] `dotnet build` / relevant test suites pass locally

<!--
Docs-only automation reminder (non-enforcement - human judgement required):

`ci.yml` skips entirely (0 runner minutes) when EVERY changed file matches its
`paths-ignore` boundary (README.md, LICENSE, docs/**). Because "Unit Tests" and
"Integration Tests" are required status checks on `main`/`develop`, a genuinely
docs-only PR will show those checks as "Expected - waiting for status to be
reported" instead of green, and will stay that way (GitHub does not auto-pass
required checks for skipped workflows).

Before merging a PR stuck in that state:
1. Confirm the diff is ACTUALLY doc-only (not just mostly-docs) - check the
   "Files changed" tab yourself; do not trust the pending state alone.
2. If genuinely doc-only and targeting `develop` (enforce_admins=false), a repo
   admin may merge with the checks pending - this is the only currently-working
   bypass path.
3. If targeting `main` (enforce_admins=true): there is currently NO way to
   satisfy or bypass a pending required check short of removing the
   `paths-ignore` skip for that PR. `workflow_dispatch` does NOT help here -
   it runs the workflow against a branch/ref you pick, it does not attach a
   status to this PR's head commit. Do not merge; escalate instead.
4. If the PR changed >=3,000 files, GitHub's path-filter evaluation is
   indeterminate (see .github/copilot-instructions.md#ci-docs-only-automation-gating)
   - treat it as NOT doc-only: split the PR, or use `workflow_dispatch` to
   manually validate the pipeline against the target branch before deciding
   how to proceed (this validates the change, it does not satisfy the PR's
   own required check).

See `.github/copilot-instructions.md` for the full gating rationale.
-->
