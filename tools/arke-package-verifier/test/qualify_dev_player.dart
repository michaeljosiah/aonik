// Read-only dev transport qualification. Uses the real Player implementation selected
// by --packages; never changes the server catalogue or the installed Android profile.
import 'dart:convert';
import 'dart:io';

import 'package:arke_library/arke_library.dart';
import 'package:arke_world/arke_world.dart';
import 'package:crypto/crypto.dart';

void require(bool value, String message) {
  if (!value) throw StateError(message);
}

Future<void> main(List<String> args) async {
  if (args.length != 3)
    throw ArgumentError(
      'withdrawn dev catalogue snapshot, reader commit, evidence output',
    );
  final original = await File(args[0]).readAsString();
  final world = parseCatalogue(original).worlds.single;
  const authority =
      'aonik-dev-api.delightfulisland-9fd7c1e7.uksouth.azurecontainerapps.io';
  require(
    world.id == 'lantern-garden-dev-403' &&
        world.package!.url.host == authority &&
        world.package!.sha256 ==
            '10047792c721bdb60da54aedf10c281265d360151e760e3a6415c323e49a362c',
    'Unexpected synthetic dev edition',
  );
  final root = await Directory.systemTemp.createTemp('aonik-player-dev-');
  final library = Directory('${root.path}/library');
  final store = LibraryStore(library);
  try {
    await store.load();
    final installer = WorldInstaller(
      store: store,
      freeBytes: (_) async => null,
    );
    await installer.install(world);
    final before = jsonEncode(store.worlds.single.toJson());
    await installer.install(world);
    require(
      jsonEncode(store.worlds.single.toJson()) == before,
      'Repeated install changed the edition',
    );
    final kept = File('${root.path}/catalogue.json');
    await kept.writeAsString(
      jsonEncode({
        'fetchedAt': DateTime.now().toUtc().toIso8601String(),
        'list': jsonDecode(original),
      }),
    );
    final hosted = HostedCatalogue(
      url: Uri.https(
        authority,
        '/publishing/channels/arke-worlds-dev/catalogue.json',
      ),
      kept: kept,
    );
    final refreshed = await hosted.refresh(force: true);
    require(
      refreshed != null && refreshed.serial >= 4 && refreshed.worlds.isEmpty,
      'Expected withdrawn dev catalogue',
    );
    require(
      (await hosted.keptList())!.serial == refreshed!.serial,
      'Forced refresh did not persist',
    );
    await store.close();
    // No HTTP operation or installer is used after reopening the library.
    final reopened = LibraryStore(library);
    String bookHash;
    try {
      await reopened.load();
      require(
        jsonEncode(reopened.worlds.single.toJson()) == before,
        'Refresh removed or changed the installed edition',
      );
      final book = reopened.items.single;
      bookHash =
          (await sha256
                  .bind(File(reopened.resolve(book.file)).openRead())
                  .first)
              .toString();
      require(
        bookHash ==
            '17f44aebaf9443e437e1e913c516fb93b924b70967c3b2910e25756622d288d4',
        'Offline book digest changed',
      );
    } finally {
      await reopened.close();
    }
    final result = {
      'checkedAt': DateTime.now().toUtc().toIso8601String(),
      'readerCommit': args[1],
      'scope':
          'Live anonymous HTTPS artifact install and forced feed refresh, then network-free library reopening; no device claim',
      'packageSha256': world.package!.sha256,
      'fingerprint': world.package!.fingerprint,
      'catalogueSerial': refreshed.serial,
      'catalogueWorldCount': refreshed.worlds.length,
      'bookSha256': bookHash,
      'repeatedInstallStable': true,
      'offlineReopened': true,
      'withdrawnImmutableUrlStillDownloadable': true,
      'passed': true,
    };
    await File(
      args[2],
    ).writeAsString('${const JsonEncoder.withIndent('  ').convert(result)}\n');
    stdout.writeln(jsonEncode(result));
  } finally {
    await store.close();
    await root.delete(recursive: true);
  }
}
