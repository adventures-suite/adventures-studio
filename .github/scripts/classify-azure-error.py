#!/usr/bin/env python3
"""Classify bounded Azure CLI stderr without retaining sensitive messages."""

from pathlib import Path
import re
import sys


MAX_ERROR_BYTES = 64 * 1024
CODE_PATTERN = re.compile(r"^[A-Za-z][A-Za-z0-9._-]{0,127}$")
PARENTHESIZED_PATTERN = re.compile(r"^ERROR:\s*\(([A-Za-z][A-Za-z0-9._-]{0,127})\)(?:\s|$)")
CODE_LINE_PATTERN = re.compile(r"^Code:\s*([A-Za-z][A-Za-z0-9._-]{0,127})\s*$")
CLASSIFICATIONS = {
    "AuthorizationFailed": "azure_authorization_failed",
    "InvalidTemplate": "azure_template_validation_failed",
    "InvalidTemplateDeployment": "azure_template_validation_failed",
    "DeploymentFailed": "azure_deployment_failed",
}


def classify(path: Path) -> tuple[str, str]:
    """Return a fail-closed classification and, when approved, one Azure code."""

    evidence = path.read_bytes()
    if len(evidence) > MAX_ERROR_BYTES:
        return "azure_error_unclassified", ""

    try:
        text = evidence.decode("utf-8")
    except UnicodeDecodeError:
        return "azure_error_unclassified", ""

    codes: set[str] = set()
    malformed_marker = False
    for line in text.splitlines():
        if line.startswith("ERROR: ("):
            match = PARENTHESIZED_PATTERN.match(line)
            if match:
                codes.add(match.group(1))
            else:
                malformed_marker = True
        if line.startswith("Code:"):
            match = CODE_LINE_PATTERN.fullmatch(line)
            if match:
                codes.add(match.group(1))
            else:
                malformed_marker = True

    if malformed_marker or len(codes) != 1:
        return "azure_error_unclassified", ""

    code = next(iter(codes))
    if not CODE_PATTERN.fullmatch(code) or code not in CLASSIFICATIONS:
        return "azure_error_unclassified", ""
    return CLASSIFICATIONS[code], code


def main() -> int:
    if len(sys.argv) != 2:
        return 2
    try:
        classification, code = classify(Path(sys.argv[1]))
    except (OSError, ValueError):
        classification, code = "azure_error_unclassified", ""
    print(classification)
    print(code)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
