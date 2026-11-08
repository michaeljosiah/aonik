// Local qualification of two actual Studio exports. No public release or device claim.
import 'dart:convert';
import 'dart:io';

import 'package:arke_library/arke_library.dart';
import 'package:arke_world/arke_world.dart';
import 'package:crypto/crypto.dart';

void require(bool condition, String message) {
  if (!condition) throw StateError(message);
}

Future<void> main(List<String> args) async {
  if (args.length != 4)
    throw ArgumentError('base ZIP, update ZIP, reader commit, evidence JSON');
  final base = File(args[0]), update = File(args[1]);
  final baseHash = (await sha256.bind(base.openRead()).first).toString();
  final updateHash = (await sha256.bind(update.openRead()).first).toString();
  require(
    baseHash ==
        '10047792c721bdb60da54aedf10c281265d360151e760e3a6415c323e49a362c',
    'Unexpected synthetic base',
  );
  require(
    updateHash ==
        'c6253edce1162fa70c1ac01f5b99d9c125c5f9c266c2375aacb2d6fc2653850b',
    'Unexpected synthetic update',
  );
  final root = await Directory.systemTemp.createTemp('aonik-player-update-');
  final store = LibraryStore(root);
  try {
    await store.load();
    final installer = WorldInstaller(
      store: store,
      freeBytes: (_) async => null,
    );
    await installer.commit(
      await installer.prepareFile(
        size: await base.length(),
        open: base.openRead,
      ),
    );
    final original = store.worlds.single;
    const locator = 'epubcfi(/6/6!/4/2/2/1:0)';
    final place = Place(
      progress: 0.5,
      updatedAt: DateTime.utc(2026, 11, 8),
      locator: locator,
    );
    final itemId = store.items.single.id;
    await store.keepPlace(itemId, place, finished: false);
    final prepared = await installer.prepareFile(
      size: await update.length(),
      open: update.openRead,
    );
    require(
      prepared.installed?.fingerprint == original.fingerprint,
      'Update did not identify installed base',
    );
    require(prepared.world.edition == '2026-11-08', 'Wrong updated edition');
    require(
      prepared.world.fingerprint != original.fingerprint,
      'Update fingerprint did not change',
    );
    await installer.commit(prepared);
    require(
      store.worlds.length == 1 && store.items.length == 1,
      'Update duplicated the library',
    );
    require(
      store.items.single.id == itemId &&
          store.items.single.place?.locator == locator &&
          store.items.single.place?.progress == 0.5,
      'Update lost reading position',
    );
    final installedFingerprint = store.worlds.single.fingerprint;
    await store.close();
    final reopened = LibraryStore(root);
    try {
      await reopened.load();
      require(
        reopened.worlds.single.fingerprint == installedFingerprint &&
            reopened.items.single.place?.locator == locator,
        'Offline reopen lost update or progress',
      );
      final bookHash =
          (await sha256
                  .bind(
                    File(
                      reopened.resolve(reopened.items.single.file),
                    ).openRead(),
                  )
                  .first)
              .toString();
      // Studio regenerates EPUB publication metadata for the new edition; compare its new verified asset hash.
      require(
        bookHash ==
            'a47f988bdf1602d1bf173c12036189161279529c0abfb3bd0ffef3964517dc5e',
        'Updated EPUB differs from the verified new export',
      );
      final evidence = {
        'checkedAt': DateTime.now().toUtc().toIso8601String(),
        'readerCommit': args[2],
        'scope':
            'Actual Studio exports through real Player file-update path; local only, no public release',
        'baseArchiveSha256': baseHash,
        'updateArchiveSha256': updateHash,
        'epubSha256': bookHash,
        'edition': reopened.worlds.single.edition,
        'readingPositionPreserved': true,
        'noDuplicateLibraryEntries': true,
        'offlineReopened': true,
        'passed': true,
      };
      await File(args[3]).writeAsString(
        '${const JsonEncoder.withIndent('  ').convert(evidence)}\n',
      );
      stdout.writeln(jsonEncode(evidence));
    } finally {
      await reopened.close();
    }
  } finally {
    await store.close();
    await root.delete(recursive: true);
  }
}
