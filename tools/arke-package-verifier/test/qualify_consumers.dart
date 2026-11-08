// Run with --packages=<Player checkout>/.dart_tool/package_config.json. This uses that
// checkout's real reader/installer, including historical readers, not a reimplementation.
// Inputs are the private synthetic outputs of PublicationDeliveryTests.ExportAsync.
import 'dart:convert';
import 'dart:io';

import 'package:arke_library/arke_library.dart';
import 'package:arke_world/arke_world.dart';
import 'package:crypto/crypto.dart';

void require(bool value, String message) {
  if (!value) throw StateError(message);
}

// Preserve every AONIK pin; redirect only the test transport to the retained test bytes.
// No public upload, credentials, certificate override or change to the production reader.
class LocalArtifacts implements HttpClient {
  LocalArtifacts(this.address, this.requests);
  final Uri address;
  final List<Uri> requests;
  final HttpClient client = HttpClient();
  @override
  set connectionTimeout(Duration? value) => client.connectionTimeout = value;
  @override
  Future<HttpClientRequest> getUrl(Uri url) {
    require(
      url.scheme == 'https' && url.host == 'public.example.test',
      'Only synthetic artifact pins may be redirected',
    );
    requests.add(url);
    return client.getUrl(address.replace(path: url.path));
  }

  @override
  void close({bool force = false}) => client.close(force: force);
  @override
  dynamic noSuchMethod(Invocation invocation) =>
      throw UnsupportedError(invocation.memberName.toString());
}

Future<void> main(List<String> args) async {
  if (args.length != 4)
    throw ArgumentError(
      'projection directory, Player checkout, current|legacy, evidence output',
    );
  final source = Directory(args[0]).absolute;
  final reader = Directory(args[1]).absolute;
  final legacy = args[2] == 'legacy';
  require(legacy || args[2] == 'current', 'Unknown reader capability profile');
  final temporary = await Directory.systemTemp.createTemp(
    'aonik-player-compatibility-',
  );
  final evidence = <Map<String, Object?>>[];
  try {
    for (final kind in [
      'world',
      'listening',
      'guide',
      'music',
      'story',
      'comic',
      'parts',
    ]) {
      final directory = Directory('${source.path}/$kind');
      final text = await File(
        '${directory.path}/catalogue.json',
      ).readAsString();
      final raw = jsonDecode(text) as Map<String, dynamic>;
      final catalogue = parseCatalogue(text);
      final world = catalogue.worlds.single;
      var pins = 0;
      Future<void> checkPins(Object? value) async {
        if (value is Map<String, dynamic>) {
          if (value['url'] case final String url) {
            final file = File(
              '${directory.path}/${Uri.parse(url).pathSegments.last}',
            );
            require(
              await file.length() == value['byteLength'],
              '$kind pin length',
            );
            require(
              (await sha256.bind(file.openRead()).first).toString() ==
                  value['sha256'],
              '$kind pin digest',
            );
            pins++;
          }
          for (final child in value.values) {
            await checkPins(child);
          }
        } else if (value is List) {
          for (final child in value) {
            await checkPins(child);
          }
        }
      }

      await checkPins(raw);
      final requests = <Uri>[];
      final server = await HttpServer.bind(InternetAddress.loopbackIPv4, 0);
      server.listen((request) async {
        final file = File('${directory.path}/${request.uri.pathSegments.last}');
        if (!await file.exists()) {
          request.response.statusCode = HttpStatus.notFound;
        } else {
          request.response.contentLength = await file.length();
          await request.response.addStream(file.openRead());
        }
        await request.response.close();
      });
      final root = Directory('${temporary.path}/$kind');
      final store = LibraryStore(root);
      await store.load();
      final installer = WorldInstaller(
        store: store,
        freeBytes: (_) async => null,
        httpClient: () => LocalArtifacts(
          Uri.parse('http://127.0.0.1:${server.port}'),
          requests,
        ),
      );
      var outcome = 'installed-and-reopened';
      try {
        if (kind == 'world' || kind == 'listening' || kind == 'parts') {
          await installer.install(world);
          final fetched = requests.length;
          await installer.install(world);
          require(
            requests.length == fetched,
            '$kind duplicate installation downloaded again',
          );
          require(
            store.worlds.single.fingerprint == world.package!.fingerprint,
            '$kind edition fingerprint',
          );
          if (kind == 'parts') {
            require(
              store.worlds.single.parts.single.fingerprint ==
                  world.package!.parts.single.fingerprint,
              'Part fingerprint',
            );
            require(
              missingParts(world, store.worlds.single).isEmpty,
              'Part continuity',
            );
            await installer.installPart(world, world.package!.parts.single);
            require(
              requests.length == fetched,
              'Repeated part downloaded again',
            );
          }
        } else if (kind == 'guide') {
          final pin = world.guide!;
          final file = File('${directory.path}/${pin.url.pathSegments.last}');
          final prepared = await installer.prepareFile(
            size: await file.length(),
            open: file.openRead,
          );
          await installer.commit(prepared);
          require(
            store.worlds.single.guide != null,
            'Guide missing after import',
          );
        } else {
          // A production needs its parent world. This base is a local synthetic test fixture;
          // the production itself is downloaded using the exact .NET-generated pin.
          final base = '${temporary.path}/$kind-base.zip';
          await packWorld(
            WorldSpec(
              id: world.id,
              edition: '2026-11-01',
              name: world.name,
              productions: [
                ProductionSpec(
                  id: 'compatibility-base',
                  title: 'Compatibility base',
                  book:
                      '${reader.path}/packages/arke_media/test/fixtures/book.epub',
                ),
              ],
            ),
            base,
          );
          final file = File(base);
          await installer.commit(
            await installer.prepareFile(
              size: await file.length(),
              open: file.openRead,
            ),
          );
          final production = world.productions.single;
          if (legacy && (kind == 'story' || kind == 'comic')) {
            require(
              production.package == null,
              'Legacy reader must not treat $kind as a generic package',
            );
            require(
              missingProductions(world, store.worlds.single).isEmpty,
              'Legacy reader scheduled unsupported $kind',
            );
            require(
              requests.isEmpty,
              'Legacy reader fetched unsupported $kind',
            );
            outcome = 'catalogue-accepted-unsupported-production-not-fetched';
          } else {
            if (kind == 'story' || kind == 'comic') {
              final future = jsonDecode(text) as Map<String, dynamic>;
              future['worlds'][0]['productions'][0][kind]['version'] = 9999;
              final newer = parseCatalogue(jsonEncode(future)).worlds.single;
              final dynamic unsupported = newer.productions.single;
              require(
                unsupported.alone == null && unsupported.needsNewerApp == true,
                'Future $kind capability was offered',
              );
              require(
                missingProductions(newer, store.worlds.single).isEmpty,
                'Future $kind scheduled a download',
              );
              require(requests.isEmpty, 'Future $kind fetched an asset');
            }
            await installer.installProduction(world, production);
            require(
              missingProductions(world, store.worlds.single).isEmpty,
              '$kind still missing after install',
            );
            final fetched = requests.length;
            await installer.installProduction(world, production);
            require(
              requests.length == fetched,
              '$kind duplicate production downloaded again',
            );
            final dynamic installed = store.worlds.single.productions
                .singleWhere((p) => p.id == production.id);
            if (kind == 'story')
              require(
                installed.story != null,
                'Installed story document absent',
              );
            if (kind == 'comic')
              require(
                installed.comic != null,
                'Installed comic document absent',
              );
          }
        }
        final held = store.worlds.single.toJson();
        await store.close();
        await server.close(force: true);
        final reopened = LibraryStore(root);
        try {
          await reopened.load();
          require(
            jsonEncode(reopened.worlds.single.toJson()) == jsonEncode(held),
            '$kind offline library changed',
          );
          for (final item in reopened.items) {
            require(
              await File(reopened.resolve(item.file)).exists(),
              '$kind offline asset missing',
            );
          }
        } finally {
          await reopened.close();
        }
        evidence.add({
          'kind': kind,
          'serial': catalogue.serial,
          'catalogueSha256': sha256.convert(utf8.encode(text)).toString(),
          'pinsVerified': pins,
          'artifactRequests': requests.length,
          'result': outcome,
          'offlineReopened': true,
        });
      } finally {
        await store.close();
        await server.close(force: true);
      }
    }
    final commit = await Process.run('git', [
      '-C',
      reader.path,
      'rev-parse',
      'HEAD',
    ]);
    require(commit.exitCode == 0, 'Cannot identify reader checkout');
    final output = {
      'checkedAt': DateTime.now().toUtc().toIso8601String(),
      'readerProfile': args[2],
      'readerCommit': commit.stdout.toString().trim(),
      'scope':
          'Real reader and installer; .NET synthetic catalogues; loopback artifact transport; no device or live release claim',
      'cases': evidence,
      'passed': true,
    };
    await File(
      args[3],
    ).writeAsString('${const JsonEncoder.withIndent('  ').convert(output)}\n');
    stdout.writeln(jsonEncode(output));
  } finally {
    await temporary.delete(recursive: true);
  }
}
