#!/usr/bin/env python3
"""Build the plugin package zip and refresh the repository manifest.

The Jellyfin plugin repository expects a manifest.json whose version entries
carry a ``sourceUrl`` pointing at a ``.zip`` file and an MD5 ``checksum`` of
that exact zip (see InstallationManager.PerformPackageInstallation). This
script produces a byte-for-byte reproducible zip so the checksum stays stable
across rebuilds, then writes the checksum into the manifest.

Usage:
    python3 tools/package.py            # package and update manifest.json
    python3 tools/package.py --check    # verify existing zip matches manifest
"""

from __future__ import annotations

import hashlib
import json
import sys
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
PLUGIN_DIR = ROOT / "src" / "Jellyfin.Plugin.PerUserVisibility"
BUILD_DIR = ROOT / "build"
DIST_DIR = ROOT / "dist"
MANIFEST = ROOT / "manifest.json"
ASSEMBLY = "Jellyfin.Plugin.PerUserVisibility.dll"
META = "meta.json"

# Fixed timestamp keeps the zip reproducible, so the MD5 does not change
# between builds of identical inputs.
FIXED_DATE = (2026, 11, 9, 0, 0, 0)


def find_assembly() -> Path:
    candidates = [
        BUILD_DIR / ASSEMBLY,
        PLUGIN_DIR / "bin" / "Release" / "net10.0" / ASSEMBLY,
    ]
    candidates += sorted((PLUGIN_DIR / "bin" / "Release").glob(f"*/{ASSEMBLY}"))
    for path in candidates:
        if path.is_file():
            return path
    sys.exit(
        f"找不到 {ASSEMBLY}。请先编译（dotnet publish -c Release），"
        f"或把它放到 {BUILD_DIR}。"
    )


def add_file(zf: zipfile.ZipFile, name: str, data: bytes) -> None:
    info = zipfile.ZipInfo(name, date_time=FIXED_DATE)
    info.compress_type = zipfile.ZIP_DEFLATED
    info.external_attr = 0o644 << 16
    zf.writestr(info, data)


def check() -> None:
    manifest = json.loads(MANIFEST.read_text(encoding="utf-8"))
    entry = manifest[0]
    for ver in entry["versions"]:
        name = ver["sourceUrl"].rsplit("/", 1)[-1]
        path = DIST_DIR / name
        if not path.is_file():
            print(f"缺失 : {path.relative_to(ROOT)}")
            sys.exit(1)
        digest = hashlib.md5(path.read_bytes()).hexdigest()
        ok = digest == ver["checksum"]
        print(f"{'一致' if ok else '不一致'}: {name}  {digest}")
        if not ok:
            sys.exit(1)


def main() -> None:
    if "--check" in sys.argv:
        check()
        return

    meta = json.loads((PLUGIN_DIR / META).read_text(encoding="utf-8"))
    version = meta["version"]
    assembly = find_assembly()

    DIST_DIR.mkdir(parents=True, exist_ok=True)
    zip_path = DIST_DIR / f"PerUserVisibility_{version}.zip"

    # Order matters for reproducibility: assembly first, then its metadata.
    with zipfile.ZipFile(zip_path, "w") as zf:
        add_file(zf, ASSEMBLY, assembly.read_bytes())
        add_file(zf, META, (PLUGIN_DIR / META).read_bytes())

    digest = hashlib.md5(zip_path.read_bytes()).hexdigest()

    manifest = json.loads(MANIFEST.read_text(encoding="utf-8"))
    entry = next(p for p in manifest if p["guid"] == meta["guid"])
    info = next(v for v in entry["versions"] if v["version"] == version)
    info["checksum"] = digest
    info["timestamp"] = meta["timestamp"]
    info["targetAbi"] = meta["targetAbi"]

    # Keep the repository-level fields in step with the plugin metadata.
    entry["name"] = meta["name"]
    entry["description"] = meta["description"]
    entry["overview"] = meta["overview"]
    entry["owner"] = meta["owner"]
    entry["category"] = meta["category"]

    MANIFEST.write_text(
        json.dumps(manifest, indent=2, ensure_ascii=False) + "\n", encoding="utf-8"
    )

    print(f"zip      : {zip_path.relative_to(ROOT)}  ({zip_path.stat().st_size} bytes)")
    print(f"md5      : {digest}")
    print(f"version  : {version}  targetAbi {info['targetAbi']}")
    print(f"manifest : {MANIFEST.relative_to(ROOT)} 已更新")


if __name__ == "__main__":
    main()
