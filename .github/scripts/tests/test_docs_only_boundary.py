"""Deterministic tests for the docs-only CI/CD gating boundary.

These tests read the REAL `paths-ignore` blocks out of
`.github/workflows/ci.yml` and `.github/workflows/deploy.yml` and verify the
resulting classification for representative changed-file sets. There is no
hand-duplicated copy of the pattern list here — if someone edits the
workflow triggers without preserving the intended boundary, these tests fail
against the actual YAML, not a stale mirror of it.

Run with:
    python -m unittest discover -s .github/scripts/tests -v
"""

from __future__ import annotations

import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from docs_only_boundary import (  # noqa: E402
    Classification,
    WORKFLOWS_DIR,
    classify,
    classify_against_workflow,
)

CI_YML = WORKFLOWS_DIR / "ci.yml"
DEPLOY_YML = WORKFLOWS_DIR / "deploy.yml"


class WorkflowFilesExistTest(unittest.TestCase):
    def test_workflow_files_present(self):
        self.assertTrue(CI_YML.is_file(), f"missing {CI_YML}")
        self.assertTrue(DEPLOY_YML.is_file(), f"missing {DEPLOY_YML}")


class CiPullRequestClassificationTest(unittest.TestCase):
    """Boundary checks against ci.yml's `pull_request.paths-ignore`."""

    def classify(self, files, diff_truncated=False):
        return classify_against_workflow(CI_YML, "pull_request", files, diff_truncated)

    def test_single_root_readme_is_docs_only(self):
        self.assertEqual(self.classify(["README.md"]), Classification.DOCS_ONLY)

    def test_nested_markdown_is_docs_only(self):
        self.assertEqual(
            self.classify(["src/HardwareStore.Core/CHANGELOG.md", "docs/guide.md"]),
            Classification.DOCS_ONLY,
        )

    def test_source_file_is_automation_required(self):
        self.assertEqual(
            self.classify(["src/HardwareStore.Api/Program.cs"]),
            Classification.AUTOMATION_REQUIRED,
        )

    def test_mixed_docs_and_code_is_automation_required(self):
        self.assertEqual(
            self.classify(["README.md", "src/HardwareStore.Api/Program.cs"]),
            Classification.AUTOMATION_REQUIRED,
        )

    def test_workflow_markdown_under_dot_github_is_automation_required(self):
        # Even though it ends in .md, anything under .github/** is
        # automation/agent-governance surface, not passive documentation.
        self.assertEqual(
            self.classify([".github/SECURITY.md"]),
            Classification.AUTOMATION_REQUIRED,
        )

    def test_markdown_under_public_deployable_output_is_automation_required(self):
        self.assertEqual(
            self.classify(["src/HardwareStore.Web/public/notes.md"]),
            Classification.AUTOMATION_REQUIRED,
        )

    def test_markdown_under_wwwroot_is_automation_required(self):
        self.assertEqual(
            self.classify(["src/HardwareStore.Api/wwwroot/help.md"]),
            Classification.AUTOMATION_REQUIRED,
        )

    def test_license_markdown_is_automation_required(self):
        self.assertEqual(self.classify(["LICENSE"]), Classification.AUTOMATION_REQUIRED)
        self.assertEqual(self.classify(["LICENSE.md"]), Classification.AUTOMATION_REQUIRED)

    def test_dist_build_output_markdown_is_automation_required(self):
        self.assertEqual(
            self.classify(["dist/README.md"]),
            Classification.AUTOMATION_REQUIRED,
        )

    def test_non_markdown_docs_like_file_is_automation_required(self):
        # Only *.md is in the passive-docs allowlist; a .txt readme is not.
        self.assertEqual(self.classify(["NOTES.txt"]), Classification.AUTOMATION_REQUIRED)

    def test_huge_diff_is_indeterminate_not_docs_only(self):
        files = [f"docs/file-{i}.md" for i in range(3000)]
        self.assertEqual(self.classify(files), Classification.INDETERMINATE)

    def test_truncated_diff_signal_is_indeterminate(self):
        self.assertEqual(
            self.classify(["README.md"], diff_truncated=True),
            Classification.INDETERMINATE,
        )

    def test_empty_diff_is_docs_only_noop(self):
        self.assertEqual(self.classify([]), Classification.DOCS_ONLY)


class DeployPushClassificationTest(unittest.TestCase):
    """Same boundary must hold for deploy.yml's `push.paths-ignore` (defense in depth)."""

    def classify(self, files, diff_truncated=False):
        return classify_against_workflow(DEPLOY_YML, "push", files, diff_truncated)

    def test_single_root_readme_is_docs_only(self):
        self.assertEqual(self.classify(["README.md"]), Classification.DOCS_ONLY)

    def test_source_file_is_automation_required(self):
        self.assertEqual(
            self.classify(["src/HardwareStore.Api/Program.cs"]),
            Classification.AUTOMATION_REQUIRED,
        )

    def test_mixed_docs_and_workflow_change_is_automation_required(self):
        self.assertEqual(
            self.classify(["README.md", ".github/workflows/deploy.yml"]),
            Classification.AUTOMATION_REQUIRED,
        )

    def test_huge_diff_is_indeterminate(self):
        files = [f"docs/file-{i}.md" for i in range(3500)]
        self.assertEqual(self.classify(files), Classification.INDETERMINATE)


class GlobEngineUnitTest(unittest.TestCase):
    """Directly test the pattern-matching primitive against known GitHub semantics."""

    def test_double_star_matches_across_directories(self):
        result = classify(
            ["a/b/c/README.md"],
            patterns=["**/*.md"],
        )
        self.assertEqual(result, Classification.DOCS_ONLY)

    def test_negation_overrides_earlier_positive_match(self):
        result = classify(
            ["docs/skip-me.md"],
            patterns=["**/*.md", "!docs/**"],
        )
        self.assertEqual(result, Classification.AUTOMATION_REQUIRED)

    def test_later_positive_reincludes_after_negation(self):
        result = classify(
            ["docs/keep-me.md"],
            patterns=["**/*.md", "!docs/**", "docs/keep-me.md"],
        )
        self.assertEqual(result, Classification.DOCS_ONLY)


if __name__ == "__main__":
    unittest.main()
