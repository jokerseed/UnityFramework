"""Validate Luban generated C# file names against Config/Luban/codegen.json prefix rules."""
from __future__ import annotations

import json
import sys
from pathlib import Path


def main() -> int:
    conf_root = Path(__file__).resolve().parent
    workspace = conf_root.parent.parent
    codegen_path = conf_root / "codegen.json"
    code_dir = workspace / "Assets" / "Generated" / "Luban"

    if not codegen_path.is_file():
        print(f"[ERROR] missing {codegen_path}")
        return 1

    cfg = json.loads(codegen_path.read_text(encoding="utf-8"))
    prefix = str(cfg.get("codeTypePrefix") or "").strip()
    exempt = {str(x) for x in cfg.get("exemptTypeNames") or []}
    enforce = bool(cfg.get("enforceOnGenerate"))

    if not prefix:
        print("[ERROR] codegen.json codeTypePrefix is empty")
        return 1

    if not code_dir.is_dir():
        print(f"[ERROR] code dir not found: {code_dir}")
        return 1

    offenders: list[str] = []
    ok: list[str] = []
    for path in sorted(code_dir.glob("*.cs")):
        stem = path.stem
        if stem in exempt:
            ok.append(f"{stem}.cs (exempt)")
            continue
        if stem.startswith(prefix):
            ok.append(f"{stem}.cs")
            continue
        offenders.append(stem + ".cs")

    print(f"[Luban] codeTypePrefix={prefix!r} enforce={enforce}")
    for line in ok:
        print(f"  OK   {line}")
    for name in offenders:
        print(f"  FAIL {name}  (expected prefix {prefix!r})")

    if offenders:
        msg = (
            f"[Luban] {len(offenders)} generated file(s) missing prefix {prefix!r}. "
            f"Rename types in Config/Luban/Defines and regenerate. See .cursor/rules/framework-luban.mdc"
        )
        if enforce:
            print(f"[ERROR] {msg}")
            return 1
        print(f"[WARN] {msg}")
        return 0

    print("[Luban] prefix check passed")
    return 0


if __name__ == "__main__":
    sys.exit(main())
