// ─────────────────────────────────────────────────────────────
//  Statement Import — DTOs & Abstract Repository
// ─────────────────────────────────────────────────────────────

/// Mirrors backend `StatementImportResponse`.
class StatementImportItem {
  const StatementImportItem({
    required this.statementImportId,
    required this.personalAccountId,
    required this.fileName,
    required this.format,
    required this.status,
    required this.rowsTotal,
    required this.rowsParsed,
    required this.rowsImported,
    required this.rowsDuplicate,
    required this.rowsFailed,
    required this.createdAt,
    this.failureReason,
    this.startedAt,
    this.completedAt,
    this.updatedAt,
  });

  final String statementImportId;
  final String personalAccountId;
  final String fileName;
  final String format;

  /// One of: `Uploaded`, `Parsed`, `Applied`, `Failed`.
  final String status;

  final int rowsTotal;
  final int rowsParsed;
  final int rowsImported;
  final int rowsDuplicate;
  final int rowsFailed;
  final String? failureReason;
  final DateTime? startedAt;
  final DateTime? completedAt;
  final DateTime createdAt;
  final DateTime? updatedAt;

  /// Number of rows that will be imported when the user taps "Apply".
  int get importableRows {
    final int importable = rowsParsed - rowsDuplicate;
    return importable < 0 ? 0 : importable;
  }

  bool get isParsed => status == 'Parsed';
  bool get isApplied => status == 'Applied';
  bool get isFailed => status == 'Failed';
}

/// Mirrors backend `StatementImportRowResponse`.
class StatementImportRowItem {
  const StatementImportRowItem({
    required this.statementImportRowId,
    required this.statementImportId,
    required this.rowNumber,
    required this.parseStatus,
    required this.createdAt,
    this.occurredAtRaw,
    this.amountRaw,
    this.descriptionRaw,
    this.merchantRaw,
    this.currencyRaw,
    this.normalizedOccurredAt,
    this.normalizedAmount,
    this.normalizedCurrency,
    this.normalizedDescription,
    this.errorMessage,
    this.fingerprint,
    this.updatedAt,
  });

  final String statementImportRowId;
  final String statementImportId;
  final int rowNumber;
  final String? occurredAtRaw;
  final String? amountRaw;
  final String? descriptionRaw;
  final String? merchantRaw;
  final String? currencyRaw;
  final DateTime? normalizedOccurredAt;
  final double? normalizedAmount;
  final String? normalizedCurrency;
  final String? normalizedDescription;

  /// One of: `Parsed`, `Failed`, `Duplicate`.
  final String parseStatus;

  final String? errorMessage;
  final String? fingerprint;
  final DateTime createdAt;
  final DateTime? updatedAt;

  bool get isParsed => parseStatus == 'Parsed';
  bool get isFailed => parseStatus == 'Failed';
  bool get isDuplicate => parseStatus == 'Duplicate';
}

/// Mirrors backend `StatementImportApplyResponse`.
class StatementImportApplyResult {
  const StatementImportApplyResult({
    required this.statementImportId,
    required this.rowsImported,
    required this.rowsDuplicate,
    required this.rowsFailed,
    required this.status,
    this.completedAt,
  });

  final String statementImportId;
  final int rowsImported;
  final int rowsDuplicate;
  final int rowsFailed;
  final String status;
  final DateTime? completedAt;
}

// ─────────────────────────────────────────────────────────────
//  Abstract Repository
// ─────────────────────────────────────────────────────────────

abstract class StatementImportRepository {
  /// Upload a CSV file for the given account.
  ///
  /// [filePath] is the absolute file-system path chosen by the user.
  /// [fileName] is the display name (e.g. `statement.csv`).
  Future<StatementImportItem> uploadStatement({
    required String personalAccountId,
    required String filePath,
    required String fileName,
  });

  /// Retrieve a single statement import by ID.
  Future<StatementImportItem?> getImport(String statementImportId);

  /// List all statement imports for the current user.
  Future<List<StatementImportItem>> listImports();

  /// List parsed rows for a given statement import.
  Future<List<StatementImportRowItem>> listImportRows(
    String statementImportId,
  );

  /// Apply the parsed import — creates `PersonalTransaction` records.
  Future<StatementImportApplyResult> applyImport(String statementImportId);
}
