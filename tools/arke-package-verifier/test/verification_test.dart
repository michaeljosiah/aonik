import 'dart:convert';
import 'dart:io';

import 'package:archive/archive.dart';
import 'package:crypto/crypto.dart';
import 'package:test/test.dart';

import '../lib/verify.dart';

final fixtures = Directory('../packages/arke_world/test/fixtures');
final media = Directory('../packages/arke_media/test/fixtures');

Map<String, dynamic> read(String path) =>
    jsonDecode(File(path).readAsStringSync()) as Map<String, dynamic>;

Map<String, List<int>> example(String kind) {
  final manifestFile = {
    'guide': 'world-guide-alone.json',
    'part': 'part-book-alone.json',
    'music': 'world-album.json',
    'story': 'world-story.json',
    'comic': 'world-comic.json',
  }[kind];
  final manifest = manifestFile == null
      ? <String, dynamic>{
          'format': 'arke-world-package',
          'schemaVersion': 1,
          'id': 'lantern-garden',
          'edition': '2026-11-03',
          'name': 'Lantern Garden',
          'productions': [
            {'id': 'story', 'title': 'Lantern Garden', 'book': 'book'},
          ],
          'assets': <String, dynamic>{
            'book': <String, dynamic>{
              'path': 'book.epub',
              'mediaType': 'application/epub+zip',
            },
          },
        }
      : read('${fixtures.path}/$kind/$manifestFile');
  final assets = manifest['assets'] as Map<String, dynamic>;
  if (kind == 'listening') {
    manifest['schemaVersion'] = 2;
    final production = (manifest['productions'] as List).first as Map;
    production.remove('book');
    production['listening'] = 'listening';
    assets.clear();
    assets['listening'] = <String, dynamic>{
      'path': 'listening.json',
      'mediaType': 'application/vnd.arke.listening+json',
    };
  }
  final documents = <String, Map<String, dynamic>>{};
  final docFile = {
    'guide': 'valid.json',
    'music': 'album.json',
    'story': 'low-water.json',
    'comic': 'low-water-drawn.json',
    'listening': 'valid.json',
  }[kind];
  if (docFile != null) {
    final available = read('${fixtures.path}/$kind/assets.json');
    final document = read('${fixtures.path}/$kind/$docFile');
    final used = <String>{};
    void visit(Object? value) {
      if (value is String && available.containsKey(value)) used.add(value);
      if (value is List)
        for (final v in value) {
          visit(v);
        }
      if (value is Map)
        for (final v in value.values) {
          visit(v);
        }
    }

    visit(document);
    for (final id in used) {
      assets.putIfAbsent(id, () => available[id]);
    }
    documents[kind] = document;
    // The upstream manifest fixture may intentionally contain unused guide illustrations.
    final fixed = <String>{kind};
    if (manifest['keyArt'] case final String id) fixed.add(id);
    for (final p in (manifest['productions'] as List)) {
      for (final role in [
        'picture',
        'tall',
        'book',
        'narration',
        'listening',
        'music',
        'story',
        'comic',
      ]) {
        if ((p as Map)[role] case final String id) fixed.add(id);
      }
    }
    assets.removeWhere(
      (key, value) => !used.contains(key) && !fixed.contains(key),
    );
  }
  final entries = <String, List<int>>{};
  for (final entry in assets.entries) {
    final a = entry.value as Map<String, dynamic>;
    final type = a['mediaType'] as String;
    final List<int> data;
    if (type.endsWith('+json')) {
      data = utf8.encode(jsonEncode(documents[entry.key] ?? documents[kind]));
    } else if (type == 'application/epub+zip') {
      data = File('${media.path}/book.epub').readAsBytesSync();
    } else if (type == 'image/jpeg') {
      data = [255, 216, 255, 224, 0, 2, 255, 217];
    } else if (type == 'image/png') {
      data = base64Decode(
        'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+j1ioAAAAASUVORK5CYII=',
      );
    } else if (type == 'video/mp4') {
      data = File('${media.path}/reel.mp4').readAsBytesSync();
    } else if (type == 'audio/mpeg') {
      data = File('${media.path}/tagged.mp3').readAsBytesSync();
    } else {
      data = File('${media.path}/chapters.m4b').readAsBytesSync();
    }
    a['byteLength'] = data.length;
    a['sha256'] = sha256.convert(data).toString();
    entries[a['path'] as String] = data;
  }
  entries['world.json'] = utf8.encode(jsonEncode(manifest));
  return entries;
}

Future<File> zip(
  Directory dir,
  Map<String, List<int>> entries, {
  bool compressed = false,
}) async {
  final archive = Archive();
  for (final e in entries.entries) {
    archive.add(
      ArchiveFile.bytes(e.key, e.value)
        ..compression = compressed
            ? CompressionType.deflate
            : CompressionType.none,
    );
  }
  return File(
    '${dir.path}/package.zip',
  ).writeAsBytes(ZipEncoder().encodeBytes(archive, level: compressed ? 6 : 0));
}

void main() {
  late Directory dir;
  setUp(() async {
    dir = await Directory.systemTemp.createTemp('aonik-verification-test-');
  });
  tearDown(() async {
    await dir.delete(recursive: true);
  });
  Future<Map<String, Object?>> check(
    File file, {
    int maxAssets = 1024,
    int maxAssetBytes = 268435456,
    int maxDocumentBytes = 4194304,
  }) async => verifyPackage(
    file.path,
    (await sha256.bind(file.openRead()).first).toString(),
    await file.length(),
    maxAssets: maxAssets,
    maxAssetBytes: maxAssetBytes,
    maxDocumentBytes: maxDocumentBytes,
    scratchRoot: dir.path,
  );

  for (final kind in [
    'world',
    'listening',
    'guide',
    'part',
    'music',
    'story',
    'comic',
  ]) {
    test('completely verifies $kind using pinned Player contracts', () async {
      final result = await check(await zip(dir, example(kind)));
      expect(result['passed'], isTrue);
      expect(
        result['kind'],
        kind == 'music'
            ? 'production'
            : kind == 'listening'
            ? 'world'
            : kind,
      );
      expect(result['readerCommit'], readerCommit);
      final presentation = result['presentation'] as Map<String, Object?>;
      expect(presentation['schemaVersion'], inInclusiveRange(1, 7));
      final manifest = presentation['manifest'] as Map<String, Object?>;
      expect(manifest['id'], result['worldId']);
      expect(manifest['edition'], result['edition']);
      expect(manifest['productions'], isA<List<Object?>>());
      final expectedManifestHash = sha256
          .convert(example(kind)['world.json']!)
          .toString();
      expect(presentation['manifestSha256'], expectedManifestHash);
    });
  }
  test('checks every asset even when the manifest structure parses', () async {
    final entries = example('world');
    entries['book.epub'] = List.filled(entries['book.epub']!.length, 42);
    await expectLater(
      check(await zip(dir, entries)),
      throwsA(
        isA<VerificationRefused>().having(
          (e) => e.code,
          'code',
          'asset-digest-mismatch',
        ),
      ),
    );
  });
  test('refuses private/unreferenced inventory even in schema 1', () async {
    final entries = example('world');
    final manifest =
        jsonDecode(utf8.decode(entries['world.json']!)) as Map<String, dynamic>;
    final secret = utf8.encode('private canon');
    (manifest['assets'] as Map)['private'] = {
      'path': 'private.txt',
      'mediaType': 'text/plain',
      'byteLength': secret.length,
      'sha256': sha256.convert(secret).toString(),
    };
    entries['private.txt'] = secret;
    entries['world.json'] = utf8.encode(jsonEncode(manifest));
    await expectLater(
      check(await zip(dir, entries)),
      throwsA(
        isA<VerificationRefused>().having(
          (e) => e.code,
          'code',
          'unreferenced-asset',
        ),
      ),
    );
  });
  for (final name in ['../escape', '/absolute', 'bad\\path', 'C:drive']) {
    test('refuses unsafe archive path $name', () async {
      final entries = example('world');
      entries[name] = [1];
      await expectLater(
        check(await zip(dir, entries)),
        throwsA(isA<VerificationRefused>()),
      );
    });
  }
  test('refuses case-colliding duplicate entries', () async {
    final entries = example('world');
    entries['BOOK.EPUB'] = entries['book.epub']!;
    await expectLater(
      check(await zip(dir, entries)),
      throwsA(
        isA<VerificationRefused>().having(
          (e) => e.code,
          'code',
          'duplicate-entry',
        ),
      ),
    );
  });
  test('refuses compressed entries before parsing document bytes', () async {
    await expectLater(
      check(await zip(dir, example('world'), compressed: true)),
      throwsA(isA<VerificationRefused>()),
    );
  });
  test('bounds document size before parsing', () async {
    await expectLater(
      check(await zip(dir, example('world')), maxDocumentBytes: 10),
      throwsA(isA<VerificationRefused>()),
    );
  });
  test('bounds media size before extraction', () async {
    await expectLater(
      check(await zip(dir, example('world')), maxAssetBytes: 1),
      throwsA(isA<VerificationRefused>()),
    );
  });
  test('bounds asset count before parsing documents', () async {
    await expectLater(
      check(await zip(dir, example('guide')), maxAssets: 1),
      throwsA(
        isA<VerificationRefused>().having(
          (e) => e.code,
          'code',
          'asset-count-limit',
        ),
      ),
    );
  });
  test('bounds nested EPUB expansion before decoding content', () async {
    final nested = Archive()
      ..add(
        ArchiveFile.bytes('payload.xml', List.filled(16384, 65))
          ..compression = CompressionType.deflate,
      );
    final payload = ZipEncoder().encodeBytes(nested, level: 9);
    final entries = example('world');
    final manifest =
        jsonDecode(utf8.decode(entries['world.json']!)) as Map<String, dynamic>;
    (manifest['assets']['book'] as Map)['byteLength'] = payload.length;
    (manifest['assets']['book'] as Map)['sha256'] = sha256
        .convert(payload)
        .toString();
    entries['book.epub'] = payload;
    entries['world.json'] = utf8.encode(jsonEncode(manifest));
    await expectLater(
      check(await zip(dir, entries), maxDocumentBytes: 1024),
      throwsA(
        isA<VerificationRefused>().having(
          (e) => e.code,
          'code',
          'epub-expansion-limit',
        ),
      ),
    );
  });
  test('refuses unknown required capabilities rather than guessing', () async {
    final entries = example('world');
    final manifest =
        jsonDecode(utf8.decode(entries['world.json']!)) as Map<String, dynamic>;
    manifest['requires'] = ['unknown-reader-capability'];
    entries['world.json'] = utf8.encode(jsonEncode(manifest));
    await expectLater(
      check(await zip(dir, entries)),
      throwsA(
        isA<VerificationRefused>().having(
          (e) => e.code,
          'code',
          'unsupported-capability',
        ),
      ),
    );
  });
  test('detects a missing asset', () async {
    final entries = example('world');
    entries.remove('book.epub');
    await expectLater(
      check(await zip(dir, entries)),
      throwsA(isA<VerificationRefused>()),
    );
  });
  test('does not leave extracted assets after failure', () async {
    final entries = example('world');
    entries['book.epub'] = [1, 2, 3];
    await expectLater(
      check(await zip(dir, entries)),
      throwsA(isA<VerificationRefused>()),
    );
    expect(dir.listSync().whereType<Directory>(), isEmpty);
  });
}
