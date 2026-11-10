import 'dart:convert';
import 'dart:io';

import 'package:arke_publication/arke_publication.dart';
import 'package:arke_world/arke_world.dart';
import 'package:crypto/crypto.dart';

// Offline audit against the real Player reader and asset checker. No network,
// library writes, decoder substitutes or publishing actions.
Future<void> main(List<String> args) async {
  final root = Directory(args.single).absolute;
  final source = File('${root.path}/hosted-catalogue.json');
  final raw = jsonDecode(await source.readAsString()) as Map<String, dynamic>;
  final catalogue = parseCatalogue(await source.readAsString());
  final worlds = (raw['worlds'] as List<dynamic>).cast<Map<String, dynamic>>();
  if (catalogue.worlds.length != worlds.length) throw StateError('World loss in parser');
  final results = <Map<String, Object?>>[];
  final failures = <Map<String, String>>[];
  final scratch = await Directory('${root.path}/scratch').create(recursive: true);

  Future<void> check(Map<String, dynamic> pin, Map<String, dynamic> world, String role, {String? production, int? partNumber}) async {
    final label = '${world['id']}/$role${production == null ? '' : '/$production'}';
    try {
      final path = '${root.path}/assets/${pin['sha256']}.zip';
      final archive = File(path);
      if (await archive.length() != pin['byteLength'] || (await sha256.bind(archive.openRead()).first).toString() != pin['sha256']) {
        throw StateError('Archive pin mismatch');
      }
      final manifest = readWorldPackage(path);
      if (manifest.id != world['id']) throw StateError('World identity mismatch: ${manifest.id}');
      if (pin['edition'] != null && manifest.edition != pin['edition']) throw StateError('Edition mismatch');
      final fingerprint = editionFingerprint(manifest);
      if (pin['fingerprint'] != null && fingerprint != pin['fingerprint']) throw StateError('Fingerprint mismatch');
      final expectedKind = switch (role) {
        'world' => !manifest.alone && manifest.part == null && !manifest.isGuideAlone,
        'guide' => manifest.isGuideAlone,
        'part' => manifest.part != null && !manifest.alone,
        'story' => manifest.alone && manifest.stories.containsKey(production) && pin['version'] == storyVersion,
        'comic' => manifest.alone && manifest.comics.containsKey(production) && pin['version'] == comicVersion,
        'production' => manifest.alone,
        _ => false,
      };
      if (!expectedKind) throw StateError('Wrong package capability');
      if (production != null && manifest.productions.single.id != production) throw StateError('Production identity mismatch');
      if (role == 'part') {
        final base = world['package'] as Map<String, dynamic>;
        final part = manifest.part!;
        if (part.follows != base['edition'] || part.fingerprint != base['fingerprint'] || part.number != partNumber) {
          throw StateError('Part continuity mismatch');
        }
        final chapters = manifest.listening[production]?.chapters ?? const <ListeningChapter>[];
        final actual = [for (final chapter in chapters) {'id': chapter.id, 'number': chapter.order, 'title': chapter.title}];
        if (jsonEncode(actual) != jsonEncode(pin['chapters'])) throw StateError('Part chapter metadata mismatch');
      }
      var total = 0;
      var largest = 0;
      final media = <String, int>{};
      final extracted = File('${scratch.path}/asset');
      for (final asset in manifest.assets.values) {
        if (await extractAsset(path, asset, extracted.path) != AssetCheck.matches) throw StateError('Inventory asset mismatch: ${asset.path}');
        total += asset.byteLength;
        if (asset.byteLength > largest) largest = asset.byteLength;
        media.update(asset.mediaType, (count) => count + 1, ifAbsent: () => 1);
        await extracted.delete();
      }
      final report = <String, Object?>{
        'worldId': manifest.id, 'role': role, 'productionId': production,
        'archiveSha256': pin['sha256'], 'archiveBytes': pin['byteLength'],
        'schemaVersion': manifest.schemaVersion, 'edition': manifest.edition,
        'fingerprint': fingerprint, 'baseFingerprint': manifest.part?.fingerprint,
        'partNumber': manifest.part?.number, 'assetCount': manifest.assets.length,
        'verifiedAssetBytes': total, 'largestAssetBytes': largest, 'mediaTypes': media,
        'passed': true,
      };
      results.add(report);
      stdout.writeln(jsonEncode({'verified': label, 'assets': manifest.assets.length}));
    } on Object catch (error) {
      failures.add({'package': label, 'error': error.toString()});
      stderr.writeln('$label: $error');
    }
  }

  for (final world in worlds) {
    final package = world['package'] as Map<String, dynamic>?;
    if (package != null) {
      await check(package, world, 'world');
      final parts = (package['parts'] as List<dynamic>? ?? []).cast<Map<String, dynamic>>();
      for (var i = 0; i < parts.length; i++) {
        await check(parts[i], world, 'part', production: parts[i]['production'] as String, partNumber: i + 1);
      }
    }
    if (world['guide'] case final Map<String, dynamic> guide) await check(guide, world, 'guide');
    for (final production in (world['productions'] as List<dynamic>).cast<Map<String, dynamic>>()) {
      for (final key in ['package', 'story', 'comic']) {
        if (production[key] case final Map<String, dynamic> pin) {
          await check(pin, world, key == 'package' ? 'production' : key, production: production['id'] as String);
        }
      }
    }
  }
  final report = {
    'checkedAtUtc': DateTime.now().toUtc().toIso8601String(), 'serial': catalogue.serial,
    'catalogueSha256': sha256.convert(await source.readAsBytes()).toString(),
    'readerCommit': 'bc2358a3baef408681397ac1baca87608b93f07c',
    'passed': failures.isEmpty, 'packages': results, 'failures': failures,
  };
  await File('${root.path}/reader-verification.json').writeAsString('${const JsonEncoder.withIndent('  ').convert(report)}\n');
  stdout.writeln(jsonEncode({'passed': results.length, 'failures': failures.length}));
  if (failures.isNotEmpty) exitCode = 1;
}
