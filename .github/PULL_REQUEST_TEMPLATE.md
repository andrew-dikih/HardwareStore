<!--
  Thanks for contributing! Fill in the sections below.
-->

## Summary

<!-- What does this PR do and why? -->

## Type of change

- [ ] Code change (bug fix, feature, refactor, tests, config, infra)
- [ ] Documentation-only change

## Docs-only classification reminder (non-enforcement)

> This checklist is a **reminder for humans, not an enforcement mechanism**.
> Nothing in CI reads or acts on these checkboxes — the actual skip/run
> decision is made by the `paths-ignore` filters in `ci.yml` / `deploy.yml`.
> See `.github/DOCS_ONLY_CI_POLICY.md` for the full policy, including why
> required status checks can show "Pending" for a genuinely docs-only PR and
> how to handle it.

If you believe this PR is documentation-only, confirm:

- [ ] The diff touches only `*.md` files.
- [ ] None of those files are under `.github/**`.
- [ ] None of those files are under `**/public/**`, `**/wwwroot/**`,
      `**/dist/**`, `**/build/**`, `**/bin/**`, or `**/obj/**`.
- [ ] None of those files are `LICENSE`, `LICENSE.md`, or `LICENSE.txt`.

If any box above is unchecked, or the diff includes anything else at all,
this is **not** a docs-only change — leave the "Documentation-only" box
above unchecked and let CI run normally.

If this PR *is* docs-only and required status checks are stuck "Pending"
after `ci.yml` correctly skips, see the "Required status checks" section of
`.github/DOCS_ONLY_CI_POLICY.md` for how to proceed (e.g. a maintainer using
"Merge without waiting for requirements to be met" on `develop`).

## Testing

<!-- How was this verified? N/A for pure documentation changes. -->
