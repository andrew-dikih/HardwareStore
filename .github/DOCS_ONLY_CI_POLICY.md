# Docs-only CI/CD gating policy

This document is the canonical explanation of how HardwareStore decides that
a change is "passive documentation" and safe to skip CI/CD for, why the
boundary is drawn where it is, and — importantly — the real limitations of
GitHub Actions that this design does **not** attempt to paper over.

## The contract

- A pull request into `main` or `develop` whose entire diff matches the
  passive-docs allowlist below **does not trigger `ci.yml` at all** —
  zero GitHub-hosted runner minutes are spent. This is enforced with a
  trigger-level `paths-ignore` filter, not a job that starts and immediately
  exits (a job that starts a runner always consumes at least one billed
  minute; a workflow that never triggers consumes none).
- A push to `main` whose entire diff matches the same allowlist **does not
  trigger `deploy.yml`** for the same reason.
- Any diff containing **at least one file outside the allowlist** — including
  every mixed docs+code change — triggers the full pipeline. GitHub's
  `paths-ignore` semantics are "skip only if *every* changed file matches",
  so this is a structural guarantee of the filter itself, not a convention
  we have to remember to uphold.

## The passive-docs allowlist (positive definition)

A file is "passive documentation" only if it is a `*.md` file **and** it is
NOT any of the following (each of these overrides the `.md` match and forces
the file back into "automation-required"):

| Excluded even if `*.md`            | Why |
|---|---|
| `.github/**`                       | Workflows, issue/PR templates, CODEOWNERS, and `copilot-instructions.md` are automation and agent-governance surface, not content. |
| `**/public/**`, `**/wwwroot/**`    | Deployable static web output — served as-is by the app. |
| `**/dist/**`, `**/build/**`, `**/bin/**`, `**/obj/**` | Build/publish artifacts. |
| `LICENSE`, `LICENSE.md`, `LICENSE.txt` | Legal/operational metadata, not narrative documentation. |

Everything else — README files anywhere in `src/`/`tests/`, changelogs,
guides, ADRs — is treated as passive.

The exact, enforced pattern list lives in `ci.yml`'s `pull_request.paths-ignore`
and is mirrored in `deploy.yml`'s `push.paths-ignore`. This document explains
the boundary; the workflow YAML is what actually executes it. A deterministic
test harness (`.github/scripts/`) parses the real YAML and asserts the
classification for representative file sets — see "Verifying the boundary"
below.

## Known GitHub Actions limitations (do not assume perfect enforcement)

### 1. Required status checks can get stuck "Pending" for legitimately-skipped workflows

`main` and `develop` both require the `Unit Tests` and `Integration Tests`
checks (from `ci.yml`) to pass before merging. When `ci.yml` is skipped by
path filtering, GitHub does **not** report those checks as passing — they
remain in a "Pending"/"Expected" state, and **a pull request that requires
those checks will be blocked from merging** by default. This is documented,
current GitHub behavior:

> "If a workflow is skipped due to path filtering ... then checks associated
> with that workflow will remain in a 'Pending' state. A pull request that
> requires those checks to be successful will be blocked from merging."
> — GitHub Docs, *Workflow syntax for GitHub Actions*

We deliberately did **not** work around this with a second "shadow" workflow
that reports the same check names as an automatic success. That pattern is
commonly suggested, but it introduces a race for mixed diffs (both the real
and the shadow workflow can fire for the same PR, and the shadow's fast
"success" can transiently overwrite/precede the real result), which conflicts
with this rollout's fail-closed requirement for mixed changes. Instead:

- On `develop`, branch protection has `enforce_admins: false`, so a
  maintainer can use GitHub's **"Merge without waiting for requirements to be
  met"** option for a PR that is genuinely docs-only. This is a conscious,
  human-in-the-loop decision, not blanket automation.
- On `main`, `enforce_admins: true` (no bypass) — but per
  `.github/copilot-instructions.md`, PRs to `main` are deployment PRs and
  are expected to require the release label + full CI regardless, so this
  case should not arise for docs-only changes in practice. If it does,
  treat it as "automation required" and let CI run rather than trying to
  force a merge.
- The PR template below reminds authors/reviewers to make this call
  explicitly. It is a **reminder, not an enforcement mechanism** — nothing
  in CI reads or acts on the checkbox.

### 2. GitHub does not evaluate path filters on very large or exotic diffs

From GitHub's own documentation:

> - If a push contains more than 1,000 commits, the workflow will **always** run.
> - If generating the diff times out, the workflow will **always** run.
> - If the generated diff contains more than 3,000 files and the files the
>   workflow filter matches are not in the first 3,000 returned by the
>   filter, the workflow will **not** run.

The first two cases fail open (safe: CI/CD still runs). The third case can
fail **closed the wrong way** — a genuinely mixed diff could, in principle,
have its automation-required files fall outside GitHub's first 3,000 and be
silently skipped. Our local classification harness treats **any diff of
>=3,000 changed files, or any diff flagged as truncated, as `INDETERMINATE`**
— never `DOCS_ONLY` — specifically so nothing in this repo's tooling makes
an unqualified "it's safe to skip" claim near that boundary.

**Operational guidance if a change ever approaches this size:**

- Split the change into smaller PRs well under 3,000 files. This is the
  correct fix in essentially all real cases — a >=3,000-file diff is not a
  normal engineering change.
- If an oversized diff has already reached `main` (e.g. via an emergency
  merge or a bulk import), **freeze the release** and manually verify/trigger
  the affected workflows (`gh workflow run ci.yml`, `gh workflow run
  deploy.yml`, or re-running the relevant checks from the Actions tab) rather
  than trusting that path filtering produced the correct decision.

## Verifying the boundary

`.github/scripts/docs_only_boundary.py` parses the actual `paths-ignore`
block out of `ci.yml` / `deploy.yml` (no hand-duplicated pattern list) and
replays GitHub's per-file, in-order, last-match-wins evaluation, including
the negation (`!pattern`) semantics.

`.github/scripts/tests/test_docs_only_boundary.py` is a deterministic
`unittest` suite that exercises this against representative file sets:
pure docs, mixed docs+code, `.github/**` markdown, deployable-output
markdown (`public/`, `wwwroot/`), `LICENSE*`, non-`.md` "docs-like" files,
and the >=3,000-file / truncated-diff indeterminate cases.

Run locally:

```sh
pip install pyyaml
python -m unittest discover -s .github/scripts/tests -v
```

This same command runs in CI whenever `.github/**` changes (see
`.github/workflows/actions-policy.yml`), so a change to the boundary that
breaks an existing guarantee (e.g. someone removes the `.github/**`
exclusion) fails the build instead of silently shipping.
