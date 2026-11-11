"""Original, stored-ZIP comic fixtures. PNG ancillary padding exercises bytes independently of pixels.

No production artwork, credentials or external downloads. Output is private test material, not a release.
"""
import argparse
import hashlib
import json
from pathlib import Path
import struct
import zipfile
import zlib


def encoded(value):
    return json.dumps(value, separators=(",", ":"), ensure_ascii=True).encode()


def chunk(kind, data):
    return struct.pack(">I", len(data)) + kind + data + struct.pack(">I", zlib.crc32(kind + data))


def picture(index):
    # An original geometric colour field; valid pixels are followed by a legal ancillary chunk.
    rows = b"".join(b"\0" + bytes(v for x in range(128) for v in
                   ((x * 2 + index) % 256, (y * 2 + index) % 256, ((x ^ y) * 2) % 256)) for y in range(128))
    return (b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", 128, 128, 8, 2, 0, 0, 0))
            + chunk(b"IDAT", zlib.compress(rows)) + chunk(b"IEND", b""))


def png_blocks(base, size):
    padding = size - len(base) - 12
    if padding < 0:
        raise ValueError("Asset allocation is too small for the original PNG")
    yield base[:-12]
    yield struct.pack(">I", padding) + b"paDd"
    crc = zlib.crc32(b"paDd")
    block = bytes(range(256)) * 4096
    while padding:
        part = block[:min(padding, len(block))]
        crc = zlib.crc32(part, crc)
        yield part
        padding -= len(part)
    yield struct.pack(">I", crc)
    yield base[-12:]


def generate(destination, target_bytes, asset_count, largest_asset, world, path_padding=0):
    if not 2 <= asset_count <= 2049:
        raise ValueError("Use 2..2049 assets (2049 is the rejection fixture)")
    if not 0 <= path_padding <= 180:
        raise ValueError("Path padding must be within 0..180 characters")
    images = asset_count - 1
    padding = ''.join('x' * min(90, path_padding - start) + '/' for start in range(0, path_padding, 90))
    document = encoded({"format": "arke-comic", "version": 1, "reading": "ltr", "aspect": "1:1", "language": "en",
                        "chapters": [{"title": "Colour Garden capacity rehearsal", "pages": [
                            {"picture": f"p{i:04d}", "alt": f"Original geometric colour field {i + 1}."} for i in range(images)]}]})
    assets = {"comic": {"path": "comic.json", "mediaType": "application/vnd.arke.comic+json",
                        "byteLength": len(document), "sha256": hashlib.sha256(document).hexdigest()}}
    for i in range(images):
        assets[f"p{i:04d}"] = {"path": f"pictures/{padding}p{i:04d}.png", "mediaType": "image/png",
                               "byteLength": largest_asset, "sha256": "0" * 64}
    manifest = {"format": "arke-world-package", "schemaVersion": 7, "id": world, "edition": "capacity-428-v1",
                "name": "Colour Garden Dev 428", "alone": True,
                "productions": [{"id": "colour-garden", "title": "Colour Garden", "picture": "p0001" if images > 1 else "p0000", "comic": "comic"}], "assets": assets}
    names = [a["path"] for a in assets.values()] + ["world.json"]
    overhead = 22 + sum(76 + 2 * len(name.encode()) for name in names)
    # Decimal byteLength fields slightly change the manifest size; converge before writing any bytes.
    for _ in range(20):
        remaining = target_bytes - overhead - len(document) - len(encoded(manifest)) - largest_asset
        lengths = [largest_asset]
        if images > 1:
            each, extra = divmod(remaining, images - 1)
            lengths += [each + (i < extra) for i in range(images - 1)]
        if any(n < 50000 or n > largest_asset for n in lengths) or (images == 1 and remaining != 0):
            raise ValueError("Target size is incompatible with the asset count and largest asset")
        previous = [assets[f"p{i:04d}"]["byteLength"] for i in range(images)]
        for i, length in enumerate(lengths):
            assets[f"p{i:04d}"]["byteLength"] = length
        if lengths == previous:
            break
    destination.parent.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(destination, "w", compression=zipfile.ZIP_STORED) as archive:
        for i, length in enumerate(lengths):
            asset = assets[f"p{i:04d}"]
            info = zipfile.ZipInfo(asset["path"], date_time=(2026, 11, 10, 0, 0, 0))
            info.file_size = length
            digest = hashlib.sha256()
            with archive.open(info, "w") as entry:
                for block in png_blocks(picture(i), length):
                    digest.update(block)
                    entry.write(block)
            asset["sha256"] = digest.hexdigest()
        for name, data in (("comic.json", document), ("world.json", encoded(manifest))):
            archive.writestr(zipfile.ZipInfo(name, date_time=(2026, 11, 10, 0, 0, 0)), data)
    if destination.stat().st_size != target_bytes:
        raise RuntimeError("Fixture ZIP size differs from the requested boundary")
    with destination.open("rb") as stream:
        digest = hashlib.file_digest(stream, "sha256").hexdigest()
    evidence = {"worldId": world, "edition": manifest["edition"], "archiveBytes": target_bytes, "archiveSha256": digest,
                "assetCount": asset_count, "largestAssetBytes": largest_asset,
                "rights": "Original synthetic geometric artwork and text; private dev capacity rehearsal only.",
                "method": "Stored ZIP, valid 128x128 PNG pixels with legal ancillary padding; no media-decoding throughput claim.",
                "rightsAssetGroups": [list(assets)[start:start + 1024] for start in range(0, asset_count, 1024)]}
    destination.with_suffix(".evidence.json").write_bytes(encoded(evidence))
    print(json.dumps({k: v for k, v in evidence.items() if k != "rightsAssetGroups"}))


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("output", type=Path)
    parser.add_argument("--bytes", type=int, required=True)
    parser.add_argument("--assets", type=int, required=True)
    parser.add_argument("--largest-asset", type=int, default=64 * 1024 * 1024)
    parser.add_argument("--world", default="colour-garden-dev-428")
    parser.add_argument("--path-padding", type=int, default=0, help="Bounded long paths exercise report-size refusal")
    args = parser.parse_args()
    generate(args.output, args.bytes, args.assets, args.largest_asset, args.world, args.path_padding)
