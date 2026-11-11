"""Produce an unapproved ownership template from already-audited public inputs.

No network, database or publication writes. This does not establish rights.
"""
import argparse
import hashlib
import json
from pathlib import Path
import zipfile


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--catalogue", type=Path, required=True)
    parser.add_argument("--expected-sha256", required=True)
    parser.add_argument("--audited-assets", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    data = args.catalogue.read_bytes()
    if len(data) > 1048576 or hashlib.sha256(data).hexdigest() != args.expected_sha256:
        raise ValueError("Baseline digest mismatch")
    root = json.loads(data)
    owners = {}
    for world in root["worlds"]:
        owner = {"CreatorPartyId": None, "EvidenceReference": None, "ProductionChapters": {}}
        pin = world.get("package")
        if pin:
            digest = pin["sha256"]
            if len(digest) != 64 or any(c not in "0123456789abcdef" for c in digest):
                raise ValueError("Invalid archive identity")
            archive = args.audited_assets / (digest + ".zip")
            with archive.open("rb") as stream:
                actual = hashlib.file_digest(stream, "sha256").hexdigest()
            if actual != digest or archive.stat().st_size != pin["byteLength"]:
                raise ValueError("Audited archive changed")
            with zipfile.ZipFile(archive) as zipped:
                def read_json(path, expected=None):
                    info = zipped.getinfo(path)
                    if info.file_size > 4 * 1024 * 1024:
                        raise ValueError("Document exceeds audit bound")
                    raw = zipped.read(info)
                    if expected and (len(raw) != expected["byteLength"] or hashlib.sha256(raw).hexdigest() != expected["sha256"]):
                        raise ValueError("Document identity mismatch")
                    return json.loads(raw)
                manifest = read_json("world.json")
                if manifest["id"] != world["id"]:
                    raise ValueError("World identity mismatch")
                for production in manifest["productions"]:
                    chapters = []
                    if production.get("listening"):
                        asset = manifest["assets"][production["listening"]]
                        listening = read_json(asset["path"], asset)
                        chapters = [{"Id": chapter["id"], "Number": chapter["order"]}
                                    for chapter in listening["chapters"]]
                    owner["ProductionChapters"][production["id"]] = chapters
        owners[world["id"]] = owner
    result = {"schemaVersion": 1, "status": "unapproved-owner-mappings-required",
              "scope": "Read-only public baseline projection; no rights attestation or production import",
              "BaselineSha256": args.expected_sha256, "Serial": root["serial"],
              "BaselineEvidenceReference": None, "BaselineOwnership": owners}
    args.output.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"serial": root["serial"], "worlds": len(owners), "status": result["status"]}))


if __name__ == "__main__":
    main()
