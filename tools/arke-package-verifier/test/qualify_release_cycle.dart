// Two-phase qualification against the isolated dev feed using an actual Player reader.
// Keep the same scratch library for both phases; this tool never publishes or withdraws.
import 'dart:convert';
import 'dart:io';

import 'package:arke_library/arke_library.dart';
import 'package:arke_world/arke_world.dart';
import 'package:crypto/crypto.dart';

void require(bool value, String message) {
  if (!value) throw StateError(message);
}

Future<void> main(List<String> args) async {
  if (args.length != 4) {
    throw ArgumentError(
      'published|withdrawn, scratch directory, reader commit, expected serial',
    );
  }
  final phase = args[0];
  require(phase == 'published' || phase == 'withdrawn', 'Unknown phase');
  final root = Directory(args[1]);
  final checkpoint = File('${root.path}/installed-checkpoint.json');
  final expectedSerial = int.parse(args[3]);
  if (phase == 'published') {
    require(
      !await root.exists(),
      'Use a fresh scratch directory for publication',
    );
    await root.create(recursive: true);
  } else {
    require(await checkpoint.exists(), 'Published phase must finish first');
  }
  final hosted = HostedCatalogue(
    url: Uri.https(
      'aonik-dev-api.delightfulisland-9fd7c1e7.uksouth.azurecontainerapps.io',
      '/publishing/channels/arke-worlds-dev/catalogue.json',
    ),
    kept: File('${root.path}/catalogue.json'),
  );
  final refreshed = await hosted.refresh(force: true);
  require(
    refreshed != null && refreshed.serial == expectedSerial,
    'Unexpected dev feed serial',
  );
  require(
    (await hosted.keptList())!.serial == expectedSerial,
    'Refresh was not persisted',
  );
  final library = Directory('${root.path}/library');
  final store = LibraryStore(library);
  String installed;
  try {
    await store.load();
    if (phase == 'published') {
      final world = refreshed!.worlds.single;
      require(
        world.id == 'lantern-garden-dev-403' &&
            world.package?.sha256 ==
                '10047792c721bdb60da54aedf10c281265d360151e760e3a6415c323e49a362c' &&
            world.package?.url.host ==
                'aonik-dev-api.delightfulisland-9fd7c1e7.uksouth.azurecontainerapps.io',
        'Unexpected approved synthetic base',
      );
      final installer = WorldInstaller(
        store: store,
        freeBytes: (_) async => null,
      );
      await installer.install(world);
      installed = jsonEncode(store.worlds.single.toJson());
      await installer.install(world);
      require(
        jsonEncode(store.worlds.single.toJson()) == installed,
        'Repeat install changed the library',
      );
      await checkpoint.writeAsString(installed);
    } else {
      require(
        refreshed!.worlds.isEmpty,
        'Withdrawal did not empty dev discovery',
      );
      installed = await checkpoint.readAsString();
      require(
        jsonEncode(store.worlds.single.toJson()) == installed,
        'Withdrawal changed installed content',
      );
    }
  } finally {
    await store.close();
  }
  // No network-capable operation occurs during this reopened-library check.
  final reopened = LibraryStore(library);
  String bookHash;
  try {
    await reopened.load();
    require(
      jsonEncode(reopened.worlds.single.toJson()) == installed,
      'Cold reopen changed installed metadata',
    );
    final book = reopened.items.single;
    bookHash =
        (await sha256.bind(File(reopened.resolve(book.file)).openRead()).first)
            .toString();
    require(
      bookHash ==
          '17f44aebaf9443e437e1e913c516fb93b924b70967c3b2910e25756622d288d4',
      'Offline EPUB digest changed',
    );
  } finally {
    await reopened.close();
  }
  final result = {
    'checkedAt': DateTime.now().toUtc().toIso8601String(),
    'readerCommit': args[2],
    'phase': phase,
    'catalogueSerial': expectedSerial,
    'worldCount': refreshed.worlds.length,
    'packageSha256':
        '10047792c721bdb60da54aedf10c281265d360151e760e3a6415c323e49a362c',
    'bookSha256': bookHash,
    'offlineReopened': true,
    'sameLibraryAcrossPhases': true,
    'scope':
        'Actual Player reader, live dev HTTPS discovery/install, persisted library; no device claim',
    'passed': true,
  };
  await File(
    '${root.path}/$phase.json',
  ).writeAsString('${const JsonEncoder.withIndent('  ').convert(result)}\n');
  stdout.writeln(jsonEncode(result));
}
