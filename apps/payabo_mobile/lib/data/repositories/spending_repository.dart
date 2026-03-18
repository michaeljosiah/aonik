// ─────────────────────────────────────────────────────────
//  SpendingRepository — interface + DTOs
//
//  Surfaces account cards, transactions (for spending_screen),
//  overview snapshots/breakdowns/metrics (for spending_overview_screen),
//  and merchant history stats (for transaction_detail_screen).
// ─────────────────────────────────────────────────────────

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
}

/// A transaction row displayed in the spending transactions sheet.
class SpendingTransaction {
  const SpendingTransaction({
    required this.id,
    required this.merchant,
    required this.category,
    required this.amountLabel,
    required this.amountMajor,
    required this.amountMinor,
    required this.currencySymbol,
    required this.isCredit,
    required this.date,
    this.iconText,
    this.iconCodePoint,
    this.iconFontFamily,
  });

  final String id;
  final String merchant;
  final String category;
  final String amountLabel;
  final String amountMajor;
  final String amountMinor;
  final String currencySymbol;
  final bool isCredit;
  final DateTime date;
  final String? iconText;
  final int? iconCodePoint;
  final String? iconFontFamily;
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
    required this.amountLabel,
    required this.iconText,
    required this.iconBackgroundKey,
    required this.iconForegroundKey,
  });

  final String merchant;
  final String category;
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
}
