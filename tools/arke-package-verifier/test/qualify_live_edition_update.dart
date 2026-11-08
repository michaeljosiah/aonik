// Live dev update qualification using a retained base library from the release rehearsal.
// Run prepare, updated, then withdrawn against the same scratch directory. No publishing.
import 'dart:convert';
import 'dart:io';

import 'package:arke_library/arke_library.dart';
import 'package:arke_world/arke_world.dart';
import 'package:crypto/crypto.dart';

const baseFingerprint =
    'aeb2150becd2b61ae6ba0f0d050356aeb09c6554381e182bdd69bac498776f03';
const updateHash =
    'c6253edce1162fa70c1ac01f5b99d9c125c5f9c266c2375aacb2d6fc2653850b';
const baseBookHash =
    '17f44aebaf9443e437e1e913c516fb93b924b70967c3b2910e25756622d288d4';
const updateBookHash =
    'a47f988bdf1602d1bf173c12036189161279529c0abfb3bd0ffef3964517dc5e';

void require(bool value, String message) {
  if (!value) throw StateError(message);
}

String position(LibraryItem item) => jsonEncode({
  'id': item.id,
  'place': item.place?.toJson(),
  'finished': item.finished,
});

Future<String> bookHash(LibraryStore store) async =>
    (await sha256
            .bind(File(store.resolve(store.items.single.file)).openRead())
            .first)
        .toString();

Future<void> main(List<String> args) async {
  if (args.length != 4) {
    throw ArgumentError(
      'prepare|updated|withdrawn, scratch directory, reader commit, expected serial',
    );
  }
  final phase = args[0], root = Directory(args[1]);
  require(['prepare', 'updated', 'withdrawn'].contains(phase), 'Unknown phase');
  final expectedSerial = int.parse(args[3]);
  final checkpoint = File('${root.path}/progress-checkpoint.json');
  final updatedCheckpoint = File('${root.path}/updated-checkpoint.json');
  require(
    await File('${root.path}/library/library.json').exists(),
    'Copy a retained base library first',
  );
  require(
    phase == 'prepare' ? !await checkpoint.exists() : await checkpoint.exists(),
    'Unexpected checkpoint state',
  );
  final hosted = HostedCatalogue(
    url: Uri.https(
      'aonik-dev-api.delightfulisland-9fd7c1e7.uksouth.azurecontainerapps.io',
      '/publishing/channels/arke-worlds-dev/catalogue.json',
    ),
    kept: File('${root.path}/catalogue.json'),
  );
  final catalogue = await hosted.refresh(force: true);
  require(
    catalogue != null && catalogue.serial == expectedSerial,
    'Unexpected live catalogue serial',
  );
  require(
    (await hosted.keptList())!.serial == expectedSerial,
    'Live catalogue not persisted',
  );
  final store = LibraryStore(Directory('${root.path}/library'));
  final steps = <String>[];
  bool detected = false, oldEditionAvailableBeforeSwap = false;
  late String expectedPosition, fingerprint, installedBookHash;
  try {
    await store.load();
    require(
      store.worlds.length == 1 && store.items.length == 1,
      'Expected one installed world and book',
    );
    if (phase == 'prepare') {
      require(
        catalogue!.worlds.isEmpty &&
            store.worlds.single.fingerprint == baseFingerprint,
        'Expected withdrawn feed and retained older edition',
      );
      require(
        await bookHash(store) == baseBookHash,
        'Retained base content changed',
      );
      // A deterministic fixture in the real store; Android UI progress is recorded separately.
      await store.keepPlace(
        store.items.single.id,
        Place(
          progress: 0.5,
          updatedAt: DateTime.now().toUtc(),
          locator: 'epubcfi(/6/6!/4/2/2/1:0)',
        ),
        finished: false,
      );
      expectedPosition = position(store.items.single);
      await checkpoint.writeAsString(expectedPosition);
      installedBookHash = baseBookHash;
    } else {
      expectedPosition = await checkpoint.readAsString();
      require(
        position(store.items.single) == expectedPosition,
        'Saved progress changed before phase',
      );
      if (phase == 'updated') {
        require(
          !await updatedCheckpoint.exists(),
          'Update phase already completed',
        );
        require(
          store.worlds.single.fingerprint == baseFingerprint,
          'Expected older installed edition',
        );
        final listed = catalogue!.worlds.single;
        require(
          listed.id == 'lantern-garden-dev-403' &&
              listed.package?.sha256 == updateHash &&
              listed.package?.edition == '2026-11-08' &&
              listed.package?.url.host ==
                  'aonik-dev-api.delightfulisland-9fd7c1e7.uksouth.azurecontainerapps.io',
          'Unexpected verified dev update',
        );
        detected = hasUpdate(listed, store.worlds.single);
        require(detected, 'Player did not detect the new live edition');
        final installer = WorldInstaller(
          store: store,
          freeBytes: (_) async => null,
        );
        await installer.update(
          listed,
          onStep: (step) => steps.add(step.toString()),
          beforeSwap: () async {
            require(
              store.worlds.single.fingerprint == baseFingerprint &&
                  position(store.items.single) == expectedPosition &&
                  await bookHash(store) == baseBookHash,
              'Old edition was changed before verified replacement',
            );
            oldEditionAvailableBeforeSwap = true;
          },
        );
        require(
          oldEditionAvailableBeforeSwap,
          'The verified update did not reach replacement',
        );
        require(
          store.worlds.length == 1 && store.items.length == 1,
          'Update duplicated the library',
        );
        require(
          store.worlds.single.edition == '2026-11-08' &&
              store.worlds.single.fingerprint != baseFingerprint &&
              !hasUpdate(listed, store.worlds.single),
          'Update did not install or clear update detection',
        );
        require(
          position(store.items.single) == expectedPosition,
          'Update changed reading progress',
        );
        final installed = jsonEncode(store.worlds.single.toJson());
        await installer.update(listed);
        require(
          jsonEncode(store.worlds.single.toJson()) == installed &&
              position(store.items.single) == expectedPosition,
          'Repeat update changed the library',
        );
        await updatedCheckpoint.writeAsString(installed);
      } else {
        require(
          catalogue!.worlds.isEmpty,
          'Withdrawal did not remove discovery',
        );
        require(
          jsonEncode(store.worlds.single.toJson()) ==
              await updatedCheckpoint.readAsString(),
          'Withdrawal changed the installed updated edition',
        );
      }
      installedBookHash = updateBookHash;
    }
    fingerprint = store.worlds.single.fingerprint!;
    require(
      await bookHash(store) == installedBookHash,
      'Installed book differs from verified export',
    );
  } finally {
    await store.close();
  }
  // Reopen with no network operation and compare exact persisted position, including its timestamp.
  final reopened = LibraryStore(Directory('${root.path}/library'));
  try {
    await reopened.load();
    require(
      reopened.worlds.length == 1 &&
          reopened.items.length == 1 &&
          reopened.worlds.single.fingerprint == fingerprint &&
          position(reopened.items.single) == expectedPosition &&
          await bookHash(reopened) == installedBookHash,
      'Offline reopen changed the edition or reading position',
    );
  } finally {
    await reopened.close();
  }
  final evidence = {
    'checkedAt': DateTime.now().toUtc().toIso8601String(),
    'readerCommit': args[2],
    'phase': phase,
    'scope':
        'Real Player reader, live dev HTTPS feed/update, deterministic saved progress; Android recorded separately',
    'catalogueSerial': expectedSerial,
    'worldCount': catalogue.worlds.length,
    'updateArchiveSha256': updateHash,
    'installedFingerprint': fingerprint,
    'bookSha256': installedBookHash,
    'position': jsonDecode(expectedPosition),
    'liveUpdateDetected': detected,
    'oldEditionAvailableBeforeSwap': oldEditionAvailableBeforeSwap,
    'progressPreserved': true,
    'noDuplicateLibraryEntries': true,
    'offlineReopened': true,
    'updateSteps': steps,
    'passed': true,
  };
  await File(
    '${root.path}/$phase.json',
  ).writeAsString('${const JsonEncoder.withIndent('  ').convert(evidence)}\n');
  stdout.writeln(jsonEncode(evidence));
}
