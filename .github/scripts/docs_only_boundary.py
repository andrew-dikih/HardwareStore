"""Shared classifier for the "docs-only" CI/CD gating boundary.

This module is the single source of truth *for verification purposes* of
whether a set of changed file paths is "passive documentation only" (safe to
skip CI/CD) or "automation-required" (must run the full pipeline). It does
not duplicate the boundary by hand — it PARSES the real `paths-ignore` list
out of the actual workflow YAML files (ci.yml / deploy.yml) so this harness
can never silently drift out of sync with what GitHub Actions will actually
do at trigger time.

GitHub Actions path-filter semantics (see .github/DOCS_ONLY_CI_POLICY.md for
citations and the full policy write-up):
  * `paths-ignore` on a trigger means: the workflow is skipped only if EVERY
    changed file matches an ignore pattern. If any file does not match, the
    workflow runs. This is what makes "mixed diffs remain automation-required"
    a structural guarantee rather than a convention.
  * Patterns are evaluated in order per file. A later pattern overrides an
    earlier one for that file. A leading `!` negates a match (forces the file
    to be treated as "not ignored", i.e. relevant/automation-required).
  * GitHub does not evaluate path filters for diffs of more than 1,000 commits
    (always runs), for diffs that time out generating (always runs), or for
    diffs with more than 3,000 changed files where the matching file(s) fall
    outside the first 3,000 returned (workflow will NOT run — a silent
    fail-open risk on GitHub's side). This harness treats >=3000 changed
    files, or an explicit "diff_truncated" signal, as INDETERMINATE rather
    than guessing — callers must not treat INDETERMINATE as "docs-only" or
    "safe to skip".
"""

from __future__ import annotations

import re
from dataclasses import dataclass
from enum import Enum
from pathlib import Path
from typing import Iterable

try:
    import yaml
except ImportError as exc:  # pragma: no cover - environment guard
    raise SystemExit(
        "PyYAML is required to run the docs-only boundary harness. "
        "Install it with `pip install pyyaml`."
    ) from exc

# GitHub applies its 3,000-file path-filter limitation at the diff level;
# mirror that ceiling so local classification never overclaims certainty.
GITHUB_PATH_FILTER_FILE_LIMIT = 3000

REPO_ROOT = Path(__file__).resolve().parents[2]
WORKFLOWS_DIR = REPO_ROOT / ".github" / "workflows"


class Classification(str, Enum):
    DOCS_ONLY = "docs_only"  # safe to skip CI/CD (workflow will not trigger)
    AUTOMATION_REQUIRED = "automation_required"  # workflow will trigger
    INDETERMINATE = "indeterminate"  # diff too large / truncated - do not assume either way


@dataclass(frozen=True)
class PatternStep:
    pattern: str
    negate: bool


_GLOB_TOKEN_RE = re.compile(r"(\*\*/|/\*\*|\*\*|\*|\?)")


def _glob_to_regex(pattern: str) -> re.Pattern:
    """Translate a GitHub Actions path-filter glob into a regex.

    Supports the doublestar conventions GitHub documents for `paths` /
    `paths-ignore`:
      * `**/` (as a prefix or after a `/`) matches zero or more whole path
        segments, so `**/*.md` matches both `README.md` (zero segments) and
        `a/b/README.md`.
      * `/**` (as a suffix or before a `/`) matches zero or more trailing
        path segments, so `dist/**` matches `dist` itself and everything
        under it, including `**/dist/**` matching a top-level `dist/x`.
      * A bare `**` (no adjacent slash) falls back to "match anything".
      * `*` matches any run of characters except `/`.
      * `?` matches a single character except `/`.
    """
    tokens = _GLOB_TOKEN_RE.split(pattern)
    regex_parts = []
    for token in tokens:
        if token == "**/":
            regex_parts.append("(?:.*/)?")
        elif token == "/**":
            regex_parts.append("(?:/.*)?")
        elif token == "**":
            regex_parts.append(".*")
        elif token == "*":
            regex_parts.append("[^/]*")
        elif token == "?":
            regex_parts.append("[^/]")
        elif token:
            regex_parts.append(re.escape(token))
    return re.compile(r"^" + "".join(regex_parts) + r"$")


def _parse_patterns(raw_patterns: Iterable[str]) -> list[PatternStep]:
    steps = []
    for raw in raw_patterns:
        negate = raw.startswith("!")
        pattern = raw[1:] if negate else raw
        steps.append(PatternStep(pattern=pattern, negate=negate))
    return steps


def load_paths_ignore(workflow_file: Path, event_name: str) -> list[str]:
    """Extract the `paths-ignore` list for a given trigger event from a workflow file."""
    with workflow_file.open("r", encoding="utf-8") as fh:
        doc = yaml.safe_load(fh)
    # YAML parses the bare key `on` as boolean True under PyYAML's default loader.
    on_block = doc.get("on", doc.get(True))
    if on_block is None:
        raise ValueError(f"{workflow_file}: no 'on' trigger block found")
    event_block = on_block.get(event_name)
    if event_block is None:
        raise ValueError(f"{workflow_file}: no '{event_name}' trigger found")
    return list(event_block.get("paths-ignore", []))


def file_is_ignored(file_path: str, patterns: list[PatternStep]) -> bool:
    """Replay GitHub's per-file, in-order, last-match-wins pattern evaluation."""
    ignored = False
    for step in patterns:
        regex = _glob_to_regex(step.pattern)
        if regex.match(file_path):
            ignored = not step.negate
    return ignored


def classify(
    changed_files: list[str],
    patterns: list[str],
    diff_truncated: bool = False,
) -> Classification:
    """Classify a changed-file set against a paths-ignore pattern list.

    Fails closed: any uncertainty (>=3000 files, or an explicit truncated-diff
    signal) returns INDETERMINATE, never DOCS_ONLY.
    """
    if diff_truncated or len(changed_files) >= GITHUB_PATH_FILTER_FILE_LIMIT:
        return Classification.INDETERMINATE
    if not changed_files:
        # GitHub: "If there are no files changed, the workflow will not run."
        return Classification.DOCS_ONLY

    steps = _parse_patterns(patterns)
    all_ignored = all(file_is_ignored(f.replace("\\", "/"), steps) for f in changed_files)
    return Classification.DOCS_ONLY if all_ignored else Classification.AUTOMATION_REQUIRED


def classify_against_workflow(
    workflow_file: Path,
    event_name: str,
    changed_files: list[str],
    diff_truncated: bool = False,
) -> Classification:
    patterns = load_paths_ignore(workflow_file, event_name)
    return classify(changed_files, patterns, diff_truncated=diff_truncated)
