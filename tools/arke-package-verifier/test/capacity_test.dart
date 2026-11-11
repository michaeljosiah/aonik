import 'dart:convert';
import 'dart:io';

import 'package:crypto/crypto.dart';
import 'package:test/test.dart';

import '../lib/verify.dart';
import 'verification_test.dart' as fixtures;

Map<String, List<int>> inventory(int count) {
  final entries = <String, List<int>>{};
  final assets = <String, Object?>{};
  final picture = base64Decode(
    'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+j1ioAAAAASUVORK5CYII=',
  );
  void add(String id, String path, String type, List<int> bytes) {
    entries[path] = bytes;
    assets[id] = {
      'path': path,
      'mediaType': type,
      'byteLength': bytes.length,
      'sha256': sha256.convert(bytes).toString(),
    };
  }
  for (var i = 0; i < count - 1; i++) {
    add('p$i', 'pictures/$i.png', 'image/png', picture);
  }
  add('comic', 'comic.json', 'application/vnd.arke.comic+json', utf8.encode(jsonEncode({
    'format': 'arke-comic', 'version': 1, 'reading': 'ltr', 'aspect': '1:1', 'language': 'en',
    'chapters': [{'title': 'Colour Garden', 'pages': [for (var i = 0; i < count - 1; i++) {'picture': 'p$i', 'alt': 'Original colour field $i.'}]}],
  })));
  entries['world.json'] = utf8.encode(jsonEncode({
    'format': 'arke-world-package', 'schemaVersion': 7, 'id': 'capacity-fixture',
    'edition': 'capacity-v1', 'name': 'Colour Garden', 'alone': true,
    'productions': [{'id': 'colour-garden', 'title': 'Colour Garden', 'picture': 'p0', 'comic': 'comic'}],
    'assets': assets,
  }));
  return entries;
}

void main() {
  late Directory dir;
  setUp(() async { dir = await Directory.systemTemp.createTemp('aonik-capacity-'); });
  tearDown(() async { await dir.delete(recursive: true); });
  Future<Map<String, Object?>> check(File file) async => verifyPackage(
    file.path, (await sha256.bind(file.openRead()).first).toString(), await file.length(),
    maxAssets: 2048, maxAssetBytes: 67108864, maxDocumentBytes: 4194304, scratchRoot: dir.path,
  );

  test('verifies every asset at the large inventory boundary within bounded evidence', () async {
    final report = await check(await fixtures.zip(dir, inventory(2048)));
    expect(report['passed'], isTrue);
    expect(report['assets'], hasLength(2048));
    final length = utf8.encode(jsonEncode(report)).length;
    expect(length, greaterThan(262144));
    expect(length, lessThan(1048576));
    expect(dir.listSync().whereType<Directory>(), isEmpty);
  }, timeout: const Timeout(Duration(minutes: 2)));

  test('refuses the first asset above the inventory boundary', () async {
    await expectLater(check(await fixtures.zip(dir, inventory(2049))),
      throwsA(isA<VerificationRefused>().having((e) => e.code, 'code', 'asset-count-limit')));
  });

  test('cached directory still checks the last asset digest and removes scratch', () async {
    final entries = inventory(64);
    entries['pictures/62.png'] = List<int>.of(entries['pictures/62.png']!)..[40] ^= 1;
    await expectLater(check(await fixtures.zip(dir, entries)),
      throwsA(isA<VerificationRefused>().having((e) => e.code, 'code', 'asset-digest-mismatch')));
    expect(dir.listSync().whereType<Directory>(), isEmpty);
  });
}
