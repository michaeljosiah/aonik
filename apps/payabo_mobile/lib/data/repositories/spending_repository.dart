// ─────────────────────────────────────────────────────────
//  SpendingRepository — interface + DTOs
//
//  Surfaces account cards, transactions (for spending_screen),
//  overview snapshots/breakdowns/metrics (for spending_overview_screen),
//  and merchant history stats (for transaction_detail_screen).
// ─────────────────────────────────────────────────────────

// ─────────────────────────────────────────────────────────
//  DTOs — shared / attachments
// ─────────────────────────────────────────────────────────

/// A file attachment associated with a transaction.
class Attachment {
  const Attachment({
    required this.id,
    required this.fileName,
    required this.mimeType,
    required this.url,
    required this.fileSizeBytes,
    required this.createdAt,
    this.thumbnailUrl,
  });

  final String id;
  final String fileName;

  /// MIME type, e.g. `'image/jpeg'`, `'application/pdf'`.
  final String mimeType;

  /// Download / display URL.
  final String url;

  /// Optional thumbnail URL for image attachments.
  final String? thumbnailUrl;

  final int fileSizeBytes;
  final DateTime createdAt;

  /// Whether this attachment is an image (JPEG, PNG, GIF, WebP, etc.).
  bool get isImage => mimeType.startsWith('image/');
}

// ─────────────────────────────────────────────────────────
//  DTOs — spending_screen
// ─────────────────────────────────────────────────────────

/// An account card shown in the spending transactions pager.
class SpendingAccountCard {
  const SpendingAccountCard({
    required this.id,
    required this.accountName,
    required this.providerName,
    required this.providerIconCodePoint,
    required this.providerIconFontFamily,
    required this.balanceLabel,
    required this.balanceMajor,
    required this.balanceMinor,
    required this.currencySymbol,
    this.currencyCode,
    this.connectionId,
    this.isManual = false,
  });

  final String id;
  final String accountName;
  final String providerName;
  final int providerIconCodePoint;
  final String providerIconFontFamily;
  final String balanceLabel;
  final String balanceMajor;
  final String balanceMinor;
  final String currencySymbol;

  /// ISO 4217 currency code (e.g. "GBP", "NGN", "USD").
  /// Available for runtime accounts; may be null for seed data that
  /// only stores the symbol.
  final String? currencyCode;

  /// The account-links connection this account belongs to.
  /// Used to filter accounts when a connection is disconnected.
  final String? connectionId;

  /// True when this is a manually-created account (not linked via
  /// open-banking). Used by the spending screen to show/hide the
  /// "Add transaction" FAB and differentiate the empty state.
  final bool isManual;
}

/// A transaction row displayed in the spending transactions sheet.
class SpendingTransaction {
  const SpendingTransaction({
    required this.id,
    required this.merchant,
    required this.category,
    this.subCategory,
    required this.amountLabel,
    required this.amountMajor,
    required this.amountMinor,
    required this.currencySymbol,
    required this.isCredit,
    required this.date,
    this.iconText,
    this.iconCodePoint,
    this.iconFontFamily,
    this.connectionId,
    this.notes,
    this.attachments = const <Attachment>[],
  });

  final String id;
  final String merchant;
  final String category;

  /// System-assigned subcategory code (e.g. `'supermarket'`, `'streaming'`).
  /// Display-only — users cannot select subcategories directly.
  final String? subCategory;
  final String amountLabel;
  final String amountMajor;
  final String amountMinor;
  final String currencySymbol;
  final bool isCredit;
  final DateTime date;
  final String? iconText;
  final int? iconCodePoint;
  final String? iconFontFamily;

  /// The account-links connection this transaction belongs to.
  final String? connectionId;

  /// Optional free-text notes the user added to this transaction.
  final String? notes;

  /// Inline convenience — primary loading is lazy via [AttachmentRepository].
  final List<Attachment> attachments;
}

// ─────────────────────────────────────────────────────────
//  DTOs — spending_overview_screen
// ─────────────────────────────────────────────────────────

/// A snapshot card for an account in the overview carousel.
///
/// Gradient and accent colours are theme-dependent. Use [gradientKey] and
/// [accentKey] to resolve colours in the UI layer via PayaboColorResolver.
/// Valid keys: `'primary'`, `'savings'`, `'bills'`.
class SpendingAccountSnapshot {
  const SpendingAccountSnapshot({
    required this.label,
    required this.balanceLabel,
    required this.statusLabel,
    required this.changeLabel,
    required this.gradientKey,
    required this.iconCodePoint,
    required this.iconFontFamily,
    this.connectionId,
  });

  final String label;
  final String balanceLabel;
  final String statusLabel;
  final String changeLabel;

  /// Key used by the screen to resolve gradient & accent from the theme.
  /// Matches [PayaboColorResolver] naming: `'primary'`, `'savings'`, `'bills'`.
  final String gradientKey;

  final int iconCodePoint;
  final String iconFontFamily;

  /// The account-links connection this snapshot belongs to.
  final String? connectionId;
}

/// A slice of the monthly breakdown pie chart.
///
/// [colorKey] is resolved by the screen via PayaboColorResolver.
/// Valid keys: `'primary'`, `'bills'`, `'success'`, `'info'`, `'other'`.
class SpendingBreakdownSlice {
  const SpendingBreakdownSlice({
    required this.label,
    required this.amountLabel,
    required this.value,
    required this.colorKey,
  });

  final String label;
  final String amountLabel;
  final double value;
  final String colorKey;
}

/// An allocation slice in the monthly overview ring.
///
/// [colorKey] is resolved by the screen via PayaboColorResolver.
class SpendingAllocationSlice {
  const SpendingAllocationSlice({
    required this.label,
    required this.amountLabel,
    required this.value,
    required this.colorKey,
  });

  final String label;
  final String amountLabel;
  final double value;
  final String colorKey;
}

/// A recent transaction preview in the overview card.
///
/// Icon colours use keys resolved by the screen:
/// `'dark'`, `'warmSurface'`, `'warmAccent'` for background,
/// `'surfaceBase'`, `'dark'`, `'warmText'` for foreground.
class SpendingRecentTransaction {
  const SpendingRecentTransaction({
    required this.merchant,
    required this.category,
    this.subCategory,
    required this.amountLabel,
    required this.iconText,
    required this.iconBackgroundKey,
    required this.iconForegroundKey,
  });

  final String merchant;
  final String category;

  /// System-assigned subcategory code. Display-only.
  final String? subCategory;
  final String amountLabel;
  final String iconText;
  final String iconBackgroundKey;
  final String iconForegroundKey;
}

/// Metric card data for the overview snapshot section.
class SpendingMetric {
  const SpendingMetric({
    required this.label,
    required this.amountLabel,
    required this.trendLabel,
    required this.iconCodePoint,
    required this.iconFontFamily,
  });

  final String label;
  final String amountLabel;
  final String trendLabel;
  final int iconCodePoint;
  final String iconFontFamily;
}

/// Chart data point for the weekly spending trend.
class SpendingTrendSpot {
  const SpendingTrendSpot({required this.x, required this.y});

  final double x;
  final double y;
}

/// Aggregated overview data returned by [SpendingRepository.getOverview].
class SpendingOverviewData {
  const SpendingOverviewData({
    required this.accountSnapshots,
    required this.totalBalanceMetric,
    required this.netWorthMetric,
    required this.safeToSpendLabel,
    required this.safeToSpendSubtitle,
    required this.breakdownSlices,
    required this.breakdownTotalLabel,
    required this.trendSummaryLabel,
    required this.trendSpots,
    required this.trendBottomLabels,
    required this.insightTitle,
    required this.insightBody,
    required this.allocationSlices,
    required this.allocationMonthLabel,
    required this.allocationYearLabel,
    required this.allocationChipLabel,
    required this.recentTransactions,
  });

  final List<SpendingAccountSnapshot> accountSnapshots;
  final SpendingMetric totalBalanceMetric;
  final SpendingMetric netWorthMetric;
  final String safeToSpendLabel;
  final String safeToSpendSubtitle;
  final List<SpendingBreakdownSlice> breakdownSlices;
  final String breakdownTotalLabel;
  final String trendSummaryLabel;
  final List<SpendingTrendSpot> trendSpots;
  final List<String> trendBottomLabels;
  final String insightTitle;
  final String insightBody;
  final List<SpendingAllocationSlice> allocationSlices;
  final String allocationMonthLabel;
  final String allocationYearLabel;
  final String allocationChipLabel;
  final List<SpendingRecentTransaction> recentTransactions;
}

// ─────────────────────────────────────────────────────────
//  DTOs — manual transaction creation
// ─────────────────────────────────────────────────────────

/// Request to create a manual transaction on a manual account.
class CreateTransactionRequest {
  const CreateTransactionRequest({
    required this.merchant,
    required this.amount,
    required this.currency,
    required this.category,
    required this.isCredit,
    required this.date,
    this.notes,
  });

  final String merchant;

  /// The transaction amount as a positive decimal (e.g. 45.99).
  final double amount;

  /// ISO currency code (e.g. 'GBP', 'NGN').
  final String currency;

  final String category;

  /// True for income / credit; false for expense / debit.
  final bool isCredit;

  final DateTime date;

  /// Optional free-text notes.
  final String? notes;
}

// ─────────────────────────────────────────────────────────
//  DTOs — transaction_detail_screen
// ─────────────────────────────────────────────────────────

/// Merchant spending history stats.
class SpendingMerchantHistory {
  const SpendingMerchantHistory({
    required this.transactionCountLabel,
    required this.averageSpendLabel,
    required this.totalSpentLabel,
  });

  final String transactionCountLabel;
  final String averageSpendLabel;
  final String totalSpentLabel;
}

// ─────────────────────────────────────────────────────────
//  Repository interface
// ─────────────────────────────────────────────────────────

abstract class SpendingRepository {
  /// Returns accounts shown in the spending transactions pager.
  Future<List<SpendingAccountCard>> getAccounts();

  /// Returns transactions for a given account.
  Future<List<SpendingTransaction>> getTransactions(String accountId);

  /// Returns the full overview data for the spending overview screen.
  Future<SpendingOverviewData> getOverview();

  /// Returns merchant history stats for the transaction detail screen.
  Future<SpendingMerchantHistory> getMerchantHistory(String merchantName);

  /// Adds a manual transaction to the given account and returns the
  /// created [SpendingTransaction]. Only meaningful for manual accounts.
  Future<SpendingTransaction> addTransaction(
    String accountId,
    CreateTransactionRequest request,
  );

  /// Updates the category of a transaction.
  Future<void> updateTransactionCategory(
    String transactionId,
    String category,
  );
}
