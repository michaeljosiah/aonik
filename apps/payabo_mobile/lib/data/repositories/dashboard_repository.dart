// ─────────────────────────────────────────────────────────
//  DashboardRepository — interface + DTOs
//
//  Surfaces upcoming bills, recent transactions, support
//  obligations, overview slices, insight cards, and key
//  financial metrics for the dashboard screen.
// ─────────────────────────────────────────────────────────

// ─────────────────────────────────────────────────────────
//  DTOs — upcoming bills & transactions
// ─────────────────────────────────────────────────────────

class DashboardUpcomingBill {
  const DashboardUpcomingBill({
    required this.id,
    required this.biller,
    required this.amountLabel,
    required this.dueDateLabel,
  });

  final String id;
  final String biller;
  final String amountLabel;
  final String dueDateLabel;
}

class DashboardTransaction {
  const DashboardTransaction({
    required this.id,
    required this.title,
    required this.amountLabel,
    required this.status,
  });

  final String id;
  final String title;
  final String amountLabel;
  final String status;
}

/// An upcoming family/community support obligation shown on the dashboard.
///
/// Kept intentionally lightweight — mirrors [DashboardUpcomingBill] so the
/// timeline can interleave both types sorted by due date.
class DashboardSupportObligation {
  const DashboardSupportObligation({
    required this.id,
    required this.beneficiaryName,
    required this.category,
    required this.amountLabel,
    required this.dueDateLabel,
    required this.frequencyLabel,
  });

  final String id;
  final String beneficiaryName;

  /// Human-readable category (e.g. "Living Expenses", "Education").
  final String category;
  final String amountLabel;
  final String dueDateLabel;
  final String frequencyLabel;
}

// ─────────────────────────────────────────────────────────
//  DTOs — overview & insight cards
// ─────────────────────────────────────────────────────────

/// A slice in the dashboard overview donut ring (e.g. Income / Expenses /
/// Investments).
///
/// [colorKey] is resolved by the screen via PayaboColorResolver.
/// Valid keys: `'success'`, `'primary'`, `'info'`.
class DashboardOverviewSlice {
  const DashboardOverviewSlice({
    required this.label,
    required this.amountLabel,
    required this.value,
    required this.colorKey,
  });

  final String label;
  final String amountLabel;
  final double value;

  /// Key the screen uses to look up the slice colour from the theme.
  final String colorKey;
}

/// Data backing the "Today's Insight" carousel card.
class DashboardTodayInsight {
  const DashboardTodayInsight({
    required this.message,
    required this.actionLabel,
    required this.timestampLabel,
  });

  /// The insight body text (e.g. "Dining spend is running 18% above …").
  final String message;

  /// CTA label (e.g. "Review dining").
  final String actionLabel;

  /// Time context label (e.g. "Today").
  final String timestampLabel;
}

/// Key financial metrics surfaced across several dashboard insight cards.
class DashboardMetrics {
  const DashboardMetrics({
    required this.spendableLabel,
    required this.spendableSubtitle,
    required this.spendableProgress,
    required this.spendableProgressLabel,
    required this.netWorthLabel,
    required this.netWorthChangeLabel,
    required this.netWorthTrendLabel,
    required this.assetsLabel,
    required this.billsLabel,
  });

  /// Formatted spendable amount (e.g. "₵1,285.00").
  final String spendableLabel;

  /// Explanation line (e.g. "After bills, savings, and your weekly buffer.").
  final String spendableSubtitle;

  /// 0.0–1.0 progress value for the spendable bar.
  final double spendableProgress;

  /// Human-readable progress (e.g. "78% free").
  final String spendableProgressLabel;

  /// Formatted net worth (e.g. "₵18,406.20").
  final String netWorthLabel;

  /// Net worth delta since last month (e.g. "+₵620").
  final String netWorthChangeLabel;

  /// Trend badge label (e.g. "up 3.5%").
  final String netWorthTrendLabel;

  /// Formatted total assets (e.g. "₵20.1k").
  final String assetsLabel;

  /// Formatted total bills (e.g. "₵1.7k").
  final String billsLabel;
}

// ─────────────────────────────────────────────────────────
//  DashboardSummary — aggregate root
// ─────────────────────────────────────────────────────────

class DashboardSummary {
  const DashboardSummary({
    required this.upcomingBills,
    required this.recentTransactions,
    this.supportObligations = const <DashboardSupportObligation>[],
    required this.overviewSlices,
    required this.overviewMonthLabel,
    required this.overviewMonthShortLabel,
    required this.overviewYearLabel,
    required this.todayInsight,
    required this.metrics,
  });

  final List<DashboardUpcomingBill> upcomingBills;
  final List<DashboardTransaction> recentTransactions;
  final List<DashboardSupportObligation> supportObligations;

  /// Slices for the overview donut ring (e.g. Income / Expenses / Investments).
  final List<DashboardOverviewSlice> overviewSlices;

  /// Full month name for the ring centre (e.g. "March").
  final String overviewMonthLabel;

  /// Abbreviated month for the chip (e.g. "Mar").
  final String overviewMonthShortLabel;

  /// Year for the ring centre (e.g. "2026").
  final String overviewYearLabel;

  /// Today's AI-generated insight card data.
  final DashboardTodayInsight todayInsight;

  /// Key financial metrics used across multiple dashboard cards.
  final DashboardMetrics metrics;
}

// ─────────────────────────────────────────────────────────
//  Repository interface
// ─────────────────────────────────────────────────────────

abstract class DashboardRepository {
  Future<DashboardSummary> getSummary();
}
