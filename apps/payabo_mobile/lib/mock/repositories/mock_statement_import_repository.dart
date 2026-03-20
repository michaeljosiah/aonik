import 'dart:math';

import '../../app/demo/demo_data_mode.dart';
import '../../data/repositories/statement_import_repository.dart';
import '../mock_behavior.dart';

class MockStatementImportRepository implements StatementImportRepository {
  MockStatementImportRepository({
    this.demoDataMode = DemoDataMode.populated,
  });

  final DemoDataMode demoDataMode;

  final List<StatementImportItem> _imports = <StatementImportItem>[];
  final Map<String, List<StatementImportRowItem>> _rows =
      <String, List<StatementImportRowItem>>{};

  int _importSequence = 0;

  // ─── Upload ──────────────────────────────────────────────
  @override
  Future<StatementImportItem> uploadStatement({
    required String personalAccountId,
    required String filePath,
    required String fileName,
  }) async {
    await MockBehavior.delay();
    MockBehavior.throwIfEnabled('statementImport.upload');

    _importSequence++;
    final String importId = 'mock-import-$_importSequence';
    final now = DateTime.now().toUtc();
    final rows = _generateMockRows(importId, now);
    _rows[importId] = rows;

    final int parsed =
        rows.where((StatementImportRowItem r) => r.isParsed).length;
    final int duplicate =
        rows.where((StatementImportRowItem r) => r.isDuplicate).length;
    final int failed =
        rows.where((StatementImportRowItem r) => r.isFailed).length;

    final item = StatementImportItem(
      statementImportId: importId,
      personalAccountId: personalAccountId,
      fileName: fileName,
      format: 'CSV',
      status: 'Parsed',
      rowsTotal: rows.length,
      rowsParsed: parsed,
      rowsImported: 0,
      rowsDuplicate: duplicate,
      rowsFailed: failed,
      createdAt: now,
      startedAt: now,
    );

    _imports.add(item);
    return item;
  }

  // ─── Get ─────────────────────────────────────────────────
  @override
  Future<StatementImportItem?> getImport(String statementImportId) async {
    await MockBehavior.delay();
    MockBehavior.throwIfEnabled('statementImport.get');

    try {
      return _imports.firstWhere(
        (StatementImportItem i) => i.statementImportId == statementImportId,
      );
    } on StateError {
      return null;
    }
  }

  // ─── List ────────────────────────────────────────────────
  @override
  Future<List<StatementImportItem>> listImports() async {
    await MockBehavior.delay();
    MockBehavior.throwIfEnabled('statementImport.list');

    return List<StatementImportItem>.from(_imports);
  }

  // ─── List Rows ───────────────────────────────────────────
  @override
  Future<List<StatementImportRowItem>> listImportRows(
    String statementImportId,
  ) async {
    await MockBehavior.delay();
    MockBehavior.throwIfEnabled('statementImport.listRows');

    return List<StatementImportRowItem>.from(
      _rows[statementImportId] ?? <StatementImportRowItem>[],
    );
  }

  // ─── Apply ───────────────────────────────────────────────
  @override
  Future<StatementImportApplyResult> applyImport(
    String statementImportId,
  ) async {
    await MockBehavior.delay();
    MockBehavior.throwIfEnabled('statementImport.apply');

    final index = _imports.indexWhere(
      (StatementImportItem i) => i.statementImportId == statementImportId,
    );
    if (index == -1) {
      throw StateError('Import $statementImportId not found');
    }

    final existing = _imports[index];
    final int imported = existing.importableRows;
    final now = DateTime.now().toUtc();

    _imports[index] = StatementImportItem(
      statementImportId: existing.statementImportId,
      personalAccountId: existing.personalAccountId,
      fileName: existing.fileName,
      format: existing.format,
      status: 'Applied',
      rowsTotal: existing.rowsTotal,
      rowsParsed: existing.rowsParsed,
      rowsImported: imported,
      rowsDuplicate: existing.rowsDuplicate,
      rowsFailed: existing.rowsFailed,
      createdAt: existing.createdAt,
      startedAt: existing.startedAt,
      completedAt: now,
    );

    return StatementImportApplyResult(
      statementImportId: statementImportId,
      rowsImported: imported,
      rowsDuplicate: existing.rowsDuplicate,
      rowsFailed: existing.rowsFailed,
      status: 'Applied',
      completedAt: now,
    );
  }

  // ─── Mock Data Generation ────────────────────────────────

  static List<StatementImportRowItem> _generateMockRows(
    String importId,
    DateTime now,
  ) {
    final random = Random(importId.hashCode);
    final int rowCount = 8 + random.nextInt(8); // 8–15 rows

    final List<String> merchants = <String>[
      'Tesco',
      'Amazon',
      'Netflix',
      'Uber',
      'Shell Petrol',
      'Starbucks',
      'Sainsbury\'s',
      'TfL',
      'Deliveroo',
      'Spotify',
      'Apple Store',
      'Primark',
      'Boots',
      'Greggs',
      'Gym Group',
    ];

    final List<String> descriptions = <String>[
      'Groceries',
      'Online purchase',
      'Subscription',
      'Ride fare',
      'Fuel',
      'Coffee',
      'Weekly shop',
      'Transport',
      'Food delivery',
      'Music subscription',
      'Electronics',
      'Clothing',
      'Pharmacy',
      'Bakery',
      'Gym membership',
    ];

    final List<StatementImportRowItem> rows = <StatementImportRowItem>[];

    for (int i = 0; i < rowCount; i++) {
      final int daysAgo = random.nextInt(30) + 1;
      final DateTime date = now.subtract(Duration(days: daysAgo));
      final double amount =
          -(random.nextDouble() * 150 + 2).roundToDouble() / 1;
      final int merchantIdx = random.nextInt(merchants.length);
      final String merchant = merchants[merchantIdx];
      final String description = descriptions[merchantIdx];

      // Make ~10% of rows duplicates, ~5% failed
      String parseStatus;
      String? errorMessage;
      if (i > 0 && random.nextDouble() < 0.10) {
        parseStatus = 'Duplicate';
      } else if (i > 0 && random.nextDouble() < 0.05) {
        parseStatus = 'Failed';
        errorMessage = 'Unable to parse date format';
      } else {
        parseStatus = 'Parsed';
      }

      rows.add(StatementImportRowItem(
        statementImportRowId: '$importId-row-${i + 1}',
        statementImportId: importId,
        rowNumber: i + 1,
        occurredAtRaw: '${date.day}/${date.month}/${date.year}',
        amountRaw: amount.toStringAsFixed(2),
        descriptionRaw: description,
        merchantRaw: merchant,
        currencyRaw: 'GBP',
        normalizedOccurredAt: parseStatus != 'Failed' ? date : null,
        normalizedAmount: parseStatus != 'Failed' ? amount : null,
        normalizedCurrency: parseStatus != 'Failed' ? 'GBP' : null,
        normalizedDescription: parseStatus != 'Failed' ? description : null,
        parseStatus: parseStatus,
        errorMessage: errorMessage,
        fingerprint:
            parseStatus != 'Failed' ? 'fp-$importId-${i + 1}' : null,
        createdAt: now,
      ));
    }

    return rows;
  }
}
