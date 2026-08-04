# GitHub Copilot Instructions

## Pull Request Targeting

All code (non-deployment) pull requests must target the `develop` branch.
Deployment pull requests (e.g. releases to staging or production) should target `main`.

## Code Style

Prefer simpler code. Favour clarity and readability over cleverness. Avoid unnecessary abstractions, over-engineering, and premature optimisation. When two solutions solve the same problem equally well, choose the simpler one.

## Architecture

Follow the SOLID principles:

- **S**ingle Responsibility Principle – each class or module should have one reason to change.
- **O**pen/Closed Principle – classes should be open for extension but closed for modification.
- **L**iskov Substitution Principle – subtypes must be substitutable for their base types.
- **I**nterface Segregation Principle – prefer small, focused interfaces over large, general-purpose ones.
- **D**ependency Inversion Principle – depend on abstractions, not concretions.

## Testing

- **Unit tests** – always write unit tests for business logic. Tests should be fast, isolated, and cover both typical and edge-case behaviour.
- **Integration tests** – always include happy-path integration tests that verify the end-to-end flow of each feature works correctly in a real or near-real environment.

## CI docs-only automation gating

`ci.yml` (PR) and `deploy.yml` (push to `main`) both use `on.<event>.paths-ignore`
to skip entirely — zero GitHub-hosted runner minutes — when **every** changed
file matches the passive-documentation boundary:

- `README.md`
- `LICENSE`
- `docs/**`

`paths-ignore` only skips a run when *all* changed files match; a single file
outside this list runs the workflow in full. The boundary is intentionally
narrow: anything under `.github/**` (workflows, this file), `src/**`,
`tests/**`, config, infra, deployable artifacts, or operational manifests is
never in the ignore list, so mixed changes and any non-doc file always remain
automation-required. This mirrors GitHub's own diff semantics — pull requests
use a three-dot diff, pushes use a two-dot diff — so no separate "is this
doc-only" logic needs to be reimplemented.

**GitHub's 3,000-file evaluation limit.** GitHub only evaluates the first
3,000 files in a generated diff against `paths`/`paths-ignore`. If a diff has
more than 3,000 files and a file that should trigger the workflow isn't among
the first 3,000 evaluated, GitHub does **not** run the workflow. This is not a
guaranteed fail-closed (safe) or fail-open (unsafe) behavior — it is
**indeterminate**: treat any change at or above this size as unclassifiable by
path filters. Split the change into smaller PRs, or manually trigger the
affected workflow via `workflow_dispatch` (both `ci.yml` and `deploy.yml`
expose it) and validate the result before merging/releasing.

**Required-status-check caveat (non-enforcement).** `main` and `develop`
branch protection require the `Unit Tests` and `Integration Tests` contexts
from `ci.yml`. When that workflow is skipped for a genuinely docs-only PR,
those checks stay "Expected — waiting for status to be reported" rather than
turning green — GitHub does not auto-pass a required check just because its
workflow was skipped by a path filter. This is deliberately **not**
auto-bypassed by automation here. The PR template
(`.github/PULL_REQUEST_TEMPLATE.md`) carries a reviewer reminder: confirm the
diff is truly doc-only before merging past a pending check (only possible on
`develop`, where `enforce_admins` is `false`), and otherwise use
`workflow_dispatch` to get a real reported status instead of bypassing.
`deploy.yml`'s jobs are not required status checks, so skipping it for
docs-only pushes to `main` carries no equivalent blocking risk.
