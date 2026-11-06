#!/usr/bin/env python3
"""Fail closed unless GameCI produced passing, non-empty NUnit 3 results."""

import argparse
from pathlib import Path
import sys
import xml.etree.ElementTree as ET


def verify_results(directory):
    if not str(directory).strip():
        raise ValueError("GameCI did not provide an artifactsPath; Unity tests are not verified.")
    directory = Path(directory)
    if not directory.is_dir():
        raise ValueError(f"Unity result directory does not exist: {directory}")
    runs = []
    for path in sorted(directory.rglob("*.xml")):
        # GameCI can also produce coverage XML. Only NUnit test-run roots count.
        try:
            root = ET.parse(path).getroot()
        except ET.ParseError as error:
            raise ValueError(f"Malformed XML result: {path}: {error}") from error
        if root.tag == "test-run":
            runs.append((path, root))
    if not runs:
        raise ValueError("No NUnit 3 test-run XML was produced; Unity tests are not verified.")
    passed = 0
    for path, root in runs:
        cases = list(root.iter("test-case"))
        successful = sum(case.get("result") == "Passed" for case in cases)
        if root.get("result") != "Passed" or any(case.get("result") == "Failed" for case in cases):
            raise ValueError(f"Unity tests did not pass: {path}")
        if not successful:
            raise ValueError(f"No passing test cases in: {path}")
        passed += successful
    return passed


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("directory", help="GameCI artifactsPath containing NUnit XML")
    args = parser.parse_args()
    try:
        passed = verify_results(args.directory)
    except (ValueError, OSError) as error:
        print(f"::error::{error}", file=sys.stderr)
        return 1
    print(f"Verified Unity test results: {passed} passed test cases.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
