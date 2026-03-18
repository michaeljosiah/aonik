import 'dart:ui';

/// Summary data for a spending category detail view.
class SpendingCategoryDetail {
  const SpendingCategoryDetail({
    required this.categoryId,
    required this.title,
    required this.iconCodePoint,
    required this.iconFontFamily,
    required this.monthLabel,
    required this.totalAmount,
    required this.deltaAmount,
    required this.deltaReference,
    required this.isDecrease,
    required this.activeAlertCount,
    required this.transactionCountLabel,
    required this.transactions,
    required this.chartCurrentMonthSpots,
    required this.chartPreviousMonthSpots,
  });

  final String categoryId;
  final String title;

  /// [IconData.codePoint] for the category icon.
  final int iconCodePoint;

  /// [IconData.fontFamily] (e.g. `'MaterialIcons'`).
  final String iconFontFamily;

  final String monthLabel;
  final String totalAmount;
  final String deltaAmount;
  final String deltaReference;
  final bool isDecrease;
  final int activeAlertCount;
  final String transactionCountLabel;
  final List<SpendingCategoryTransaction> transactions;

  /// Chart data for the current month as `[x, y]` pairs.
  final List<List<double>> chartCurrentMonthSpots;

  /// Chart data for the previous month as `[x, y]` pairs.
  final List<List<double>> chartPreviousMonthSpots;
}

/// A single transaction inside a spending category detail.
class SpendingCategoryTransaction {
  const SpendingCategoryTransaction({
    required this.dateLabel,
    required this.merchant,
    required this.amount,
    required this.time,
    required this.accountName,
    required this.accountBadge,
    required this.avatarLabel,
    required this.avatarBackgroundValue,
    required this.avatarForegroundValue,
  });

  final String dateLabel;
  final String merchant;
  final String amount;
  final String time;
  final String accountName;
  final String accountBadge;
  final String avatarLabel;

  /// ARGB colour value for the avatar background (e.g. `0xFF1A1C20`).
  final int avatarBackgroundValue;

  /// ARGB colour value for the avatar foreground (e.g. `0xFF4ACB64`).
  final int avatarForegroundValue;

  Color get avatarBackground => Color(avatarBackgroundValue);
  Color get avatarForeground => Color(avatarForegroundValue);
}

abstract class SpendingCategoryRepository {
  /// Returns the category detail for the given [categoryId], or `null` if the
  /// category is unknown.
  Future<SpendingCategoryDetail?> getCategoryDetail(String categoryId);
}
