import 'dart:convert';
import 'dart:io';

import 'package:archive/archive_io.dart';
import 'package:arke_media/arke_media.dart';
import 'package:arke_publication/arke_publication.dart';
import 'package:arke_world/arke_world.dart';
import 'package:crypto/crypto.dart';

const readerCommit = '27df122566bcdef18126932c15a74112e18b8573';

class VerificationRefused implements Exception {
  const VerificationRefused(this.code);
  final String code;
}

Map<String, Object?> refused(String code) => {
  'passed': false,
  'code': code,
  'readerCommit': readerCommit,
  'archiveSha256': '',
  'archiveBytes': 0,
  'worldId': '',
  'edition': '',
  'fingerprint': '',
  'kind': '',
  'baseFingerprint': null,
  'partNumber': null,
  'coverAssetId': null,
  'assets': <Object?>[],
  'presentation': null,
};

Future<Map<String, Object?>> verifyPackage(
  String path,
  String expectedHash,
  int expectedBytes, {
  required int maxAssets,
  required int maxAssetBytes,
  required int maxDocumentBytes,
  required String scratchRoot,
}) async {
  if (expectedBytes <= 0 ||
      maxAssets < 1 ||
      maxAssets > 2048 ||
      maxAssetBytes < 1 ||
      maxDocumentBytes < 1)
    throw const VerificationRefused('invalid-worker-limits');
  final package = File(path);
  String manifestSha256 = '';
  if (await package.length() != expectedBytes ||
      (await sha256.bind(package.openRead()).first).toString() != expectedHash)
    throw const VerificationRefused('archive-digest-mismatch');
  final source = InputFileStream(path);
  try {
    final decoder = ZipDecoder();
    final archive = decoder.decodeStream(source);
    if (decoder.directory.fileHeaders.length > maxAssets * 4 + 64)
      throw const VerificationRefused('entry-count-limit');
    final seen = <String>{};
    for (final entry in decoder.directory.fileHeaders) {
      final name = entry.filename;
      if (name.startsWith('/') ||
          name.contains(r'\') ||
          name.contains(':') ||
          name.split('/').any((s) => s == '..' || s == '.') ||
          (entry.externalFileAttributes >> 16) & 0xF000 == 0xA000)
        throw const VerificationRefused('unsafe-archive-path');
      if (!seen.add(name.toLowerCase()))
        throw const VerificationRefused('duplicate-entry');
      if (!name.endsWith('/') && entry.compressionMethod != 0)
        throw const VerificationRefused('compressed-entry');
    }
    final manifest = archive.find('world.json');
    if (manifest == null || manifest.size > maxDocumentBytes)
      throw const VerificationRefused('manifest-size-limit');
    manifestSha256 = sha256.convert(manifest.readBytes()!).toString();
    final raw =
        jsonDecode(utf8.decode(manifest.readBytes()!)) as Map<String, Object?>;
    final assets = raw['assets'] as Map<String, Object?>;
    if (assets.length > maxAssets)
      throw const VerificationRefused('asset-count-limit');
    for (final asset in assets.values.cast<Map<String, Object?>>()) {
      final length = asset['byteLength'];
      if (length is! int || length < 0 || length > maxAssetBytes)
        throw const VerificationRefused('asset-size-limit');
      final type = asset['mediaType'];
      if (type is String && type.endsWith('+json') && length > maxDocumentBytes)
        throw const VerificationRefused('document-size-limit');
    }
  } finally {
    source.closeSync();
  }

  // The owning reader enforces the world/document capability contracts. It does NOT hash all media.
  final WorldManifest manifest;
  try {
    manifest = readWorldPackage(path);
  } on WorldPackageRefused catch (error) {
    throw VerificationRefused(
      error.refusal == WorldRefusal.newerArke
          ? 'unsupported-capability'
          : error.reason.startsWith('unused assets')
          ? 'unreferenced-asset'
          : 'package-refused',
    );
  }
  final used = {
    ...manifest.namedAssets,
    ...?manifest.guideDocument?.assetIds,
    for (final d in manifest.listening.values) ...d.assetIds,
    for (final d in manifest.music.values) ...d.assetIds,
    for (final d in manifest.stories.values) ...d.assetIds,
    for (final d in manifest.comics.values) ...d.assetIds,
  };
  if (manifest.assets.keys.any((id) => !used.contains(id)))
    throw const VerificationRefused('unreferenced-asset');
  final scratch = await Directory(scratchRoot).createTemp('verification-');
  // Reuse the bounded ZIP directory. Reopening it per asset makes high-cardinality packages quadratic.
  // The pinned reader still validates the package contract, and its checkAsset verifies every extracted byte.
  final assetInput = InputFileStream(path);
  try {
    final entries = {
      for (final entry in ZipDecoder().decodeStream(assetInput)) entry.name: entry,
    };
    for (final asset in manifest.assets.values) {
      final extracted =
          '${scratch.path}/asset'; // NEVER use an untrusted archive path as a destination.
      final result = await _extractCheckedAsset(entries[asset.path], asset, extracted);
      if (result != AssetCheck.matches)
        throw const VerificationRefused('asset-digest-mismatch');
      if (asset.mediaType == 'application/epub+zip') {
        _boundEpub(extracted, maxDocumentBytes);
        readMediaInfo(extracted, FileFormat.epub);
      } else if ({
        'audio/mp4',
        'audio/x-m4b',
        'video/mp4',
      }.contains(asset.mediaType)) {
        readMediaInfo(extracted, FileFormat.m4b);
      } else if (asset.mediaType == 'audio/mpeg') {
        readMediaInfo(extracted, FileFormat.mp3);
      } else if (pictureMediaTypes.contains(asset.mediaType)) {
        final file = File(extracted).openSync();
        try {
          final header = file.readSync(8);
          final png =
              header.length == 8 &&
              const ListEquality().equals(header, [
                137,
                80,
                78,
                71,
                13,
                10,
                26,
                10,
              ]);
          final jpeg =
              header.length >= 3 &&
              header[0] == 255 &&
              header[1] == 216 &&
              header[2] == 255;
          if ((asset.mediaType == 'image/png' && !png) ||
              (asset.mediaType == 'image/jpeg' && !jpeg))
            throw const VerificationRefused('image-type-mismatch');
        } finally {
          file.closeSync();
        }
      }
      await File(extracted).delete();
    }
  } finally {
    assetInput.closeSync();
    await scratch.delete(recursive: true);
  }
  final kind = manifest.part != null
      ? 'part'
      : manifest.isGuideAlone
      ? 'guide'
      : manifest.comics.isNotEmpty
      ? 'comic'
      : manifest.stories.isNotEmpty
      ? 'story'
      : manifest.alone
      ? 'production'
      : 'world';
  return {
    'passed': true,
    'code': 'verified',
    'readerCommit': readerCommit,
    'archiveSha256': expectedHash,
    'archiveBytes': expectedBytes,
    'worldId': manifest.id,
    'edition': manifest.edition,
    'fingerprint': editionFingerprint(manifest),
    'kind': kind,
    'baseFingerprint': manifest.part?.fingerprint,
    'partNumber': manifest.part?.number,
    'coverAssetId':
        manifest.keyArt ??
        (manifest.productions.isEmpty
            ? null
            : manifest.productions.first.picture),
    'assets': [
      for (final e in manifest.assets.entries)
        {'id': e.key, ...e.value.toJson()},
    ],
    'presentation': {
      'schemaVersion': manifest.schemaVersion,
      'manifestSha256': manifestSha256,
      'manifest': manifest.toJson(),
      'details': {
        for (final p in manifest.productions)
          p.id: {
            if (manifest.music[p.id] != null)
              'musicKind': manifest.music[p.id]!.kind.name,
            if (manifest.stories[p.id] != null) 'storyVersion': storyVersion,
            if (manifest.comics[p.id] != null) 'comicVersion': comicVersion,
            if (manifest.listening[p.id] != null)
              'chapters': [
                for (final c in manifest.listening[p.id]!.chapters)
                  {'id': c.id, 'number': c.order, 'title': c.title},
              ],
          },
      },
    },
  };
}

Future<AssetCheck> _extractCheckedAsset(
  ArchiveFile? entry,
  WorldAsset asset,
  String destination,
) async {
  if (entry == null || !entry.isFile) return AssetCheck.missing;
  if (entry.size != asset.byteLength) return AssetCheck.wrongLength;
  final output = OutputFileStream(destination);
  try {
    entry.writeContent(output);
  } finally {
    await output.close();
  }
  return checkAsset(asset.asPublicationAsset, () => File(destination).openRead());
}

// Avoid a new package dependency for an eight-byte signature comparison.
class ListEquality {
  const ListEquality();
  bool equals(List<int> a, List<int> b) =>
      a.length == b.length &&
      Iterable<int>.generate(a.length).every((i) => a[i] == b[i]);
}

void _boundEpub(String path, int maxDocumentBytes) {
  final input = InputFileStream(path);
  try {
    final decoder = ZipDecoder();
    decoder.decodeStream(input);
    if (decoder.directory.fileHeaders.length > 2048)
      throw const VerificationRefused('epub-entry-limit');
    var total = 0;
    for (final e in decoder.directory.fileHeaders) {
      total += e.uncompressedSize;
      if (e.uncompressedSize > maxDocumentBytes ||
          total > maxDocumentBytes * 16)
        throw const VerificationRefused('epub-expansion-limit');
    }
  } finally {
    input.closeSync();
  }
}
