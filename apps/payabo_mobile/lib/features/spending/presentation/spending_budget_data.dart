import 'package:flutter/material.dart';
import 'package:intl/intl.dart';

import '../../../shared/theme/payabo_colors.dart';

const String spendingBudgetMonthLabel = 'March 2026';

const List<SpendingBudgetCategory> spendingBudgetCategories =
    <SpendingBudgetCategory>[
  SpendingBudgetCategory(
    id: 'housing',
    name: 'Housing',
    icon: Icons.home_work_outlined,
    accentColor: PayaboColors.primary,
    linkedSpendingCategoryId: 'finances',
    lineItems: <SpendingBudgetLineItem>[
      SpendingBudgetLineItem(
        id: 'rent',
        name: 'Rent',
        allocated: 850,
        spent: 850,
      ),
      SpendingBudgetLineItem(
        id: 'repairs',
        name: 'Repairs',
        allocated: 100,
        spent: 42,
      ),
      SpendingBudgetLineItem(
        id: 'supplies',
        name: 'Supplies',
        allocated: 250,
        spent: 58,
      ),
    ],
    history: <SpendingBudgetHistoryPoint>[
      SpendingBudgetHistoryPoint(label: 'Jan', amount: 1105),
      SpendingBudgetHistoryPoint(label: 'Feb', amount: 1182),
      SpendingBudgetHistoryPoint(label: 'Mar', amount: 950, isCurrent: true),
      SpendingBudgetHistoryPoint(label: 'Apr', amount: 1135),
      SpendingBudgetHistoryPoint(label: 'May', amount: 1090),
      SpendingBudgetHistoryPoint(label: 'Jun', amount: 1210),
      SpendingBudgetHistoryPoint(label: 'Jul', amount: 1174),
      SpendingBudgetHistoryPoint(label: 'Aug', amount: 1120),
      SpendingBudgetHistoryPoint(label: 'Sep', amount: 1194),
      SpendingBudgetHistoryPoint(label: 'Oct', amount: 1160),
      SpendingBudgetHistoryPoint(label: 'Nov', amount: 1206),
      SpendingBudgetHistoryPoint(label: 'Dec', amount: 1188),
    ],
  ),
  SpendingBudgetCategory(
    id: 'groceries',
    name: 'Food & Groceries',
    icon: Icons.local_grocery_store_outlined,
    accentColor: PayaboColors.success,
    linkedSpendingCategoryId: 'groceries',
    lineItems: <SpendingBudgetLineItem>[
      SpendingBudgetLineItem(
        id: 'supermarket',
        name: 'Supermarket',
        allocated: 350,
        spent: 240,
      ),
      SpendingBudgetLineItem(
        id: 'market',
        name: 'Fresh market',
        allocated: 150,
        spent: 123.65,
      ),
      SpendingBudgetLineItem(
        id: 'snacks',
        name: 'Coffee & snacks',
        allocated: 100,
        spent: 55,
      ),
    ],
    history: <SpendingBudgetHistoryPoint>[
      SpendingBudgetHistoryPoint(label: 'Jan', amount: 482),
      SpendingBudgetHistoryPoint(label: 'Feb', amount: 640),
      SpendingBudgetHistoryPoint(label: 'Mar', amount: 418.65, isCurrent: true),
      SpendingBudgetHistoryPoint(label: 'Apr', amount: 532),
      SpendingBudgetHistoryPoint(label: 'May', amount: 605),
      SpendingBudgetHistoryPoint(label: 'Jun', amount: 448),
      SpendingBudgetHistoryPoint(label: 'Jul', amount: 590),
      SpendingBudgetHistoryPoint(label: 'Aug', amount: 470),
      SpendingBudgetHistoryPoint(label: 'Sep', amount: 515),
      SpendingBudgetHistoryPoint(label: 'Oct', amount: 462),
      SpendingBudgetHistoryPoint(label: 'Nov', amount: 488),
      SpendingBudgetHistoryPoint(label: 'Dec', amount: 575),
    ],
  ),
  SpendingBudgetCategory(
    id: 'transport',
    name: 'Transport',
    icon: Icons.directions_bus_outlined,
    accentColor: PayaboColors.warning,
    linkedSpendingCategoryId: 'transport',
    lineItems: <SpendingBudgetLineItem>[
      SpendingBudgetLineItem(
        id: 'fuel',
        name: 'Fuel',
        allocated: 200,
        spent: 232.2,
      ),
      SpendingBudgetLineItem(
        id: 'ride-apps',
        name: 'Ride apps',
        allocated: 120,
        spent: 106,
      ),
      SpendingBudgetLineItem(
        id: 'public-transit',
        name: 'Public transit',
        allocated: 100,
        spent: 110,
      ),
    ],
    history: <SpendingBudgetHistoryPoint>[
      SpendingBudgetHistoryPoint(label: 'Jan', amount: 290),
      SpendingBudgetHistoryPoint(label: 'Feb', amount: 495),
      SpendingBudgetHistoryPoint(label: 'Mar', amount: 448.2, isCurrent: true),
      SpendingBudgetHistoryPoint(label: 'Apr', amount: 410),
      SpendingBudgetHistoryPoint(label: 'May', amount: 452),
      SpendingBudgetHistoryPoint(label: 'Jun', amount: 335),
      SpendingBudgetHistoryPoint(label: 'Jul', amount: 472),
      SpendingBudgetHistoryPoint(label: 'Aug', amount: 360),
      SpendingBudgetHistoryPoint(label: 'Sep', amount: 398),
      SpendingBudgetHistoryPoint(label: 'Oct', amount: 386),
      SpendingBudgetHistoryPoint(label: 'Nov', amount: 440),
      SpendingBudgetHistoryPoint(label: 'Dec', amount: 374),
    ],
  ),
  SpendingBudgetCategory(
    id: 'utilities',
    name: 'Utilities',
    icon: Icons.lightbulb_outline_rounded,
    accentColor: PayaboColors.info,
    linkedSpendingCategoryId: 'finances',
    lineItems: <SpendingBudgetLineItem>[
      SpendingBudgetLineItem(
        id: 'electricity',
        name: 'Electricity',
        allocated: 180,
        spent: 120.4,
      ),
      SpendingBudgetLineItem(
        id: 'water',
        name: 'Water',
        allocated: 70,
        spent: 48.9,
      ),
      SpendingBudgetLineItem(
        id: 'internet',
        name: 'Internet',
        allocated: 130,
        spent: 72,
      ),
    ],
    history: <SpendingBudgetHistoryPoint>[
      SpendingBudgetHistoryPoint(label: 'Jan', amount: 324),
      SpendingBudgetHistoryPoint(label: 'Feb', amount: 360),
      SpendingBudgetHistoryPoint(label: 'Mar', amount: 241.3, isCurrent: true),
      SpendingBudgetHistoryPoint(label: 'Apr', amount: 312),
      SpendingBudgetHistoryPoint(label: 'May', amount: 388),
      SpendingBudgetHistoryPoint(label: 'Jun', amount: 301),
      SpendingBudgetHistoryPoint(label: 'Jul', amount: 376),
      SpendingBudgetHistoryPoint(label: 'Aug', amount: 296),
      SpendingBudgetHistoryPoint(label: 'Sep', amount: 348),
      SpendingBudgetHistoryPoint(label: 'Oct', amount: 314),
      SpendingBudgetHistoryPoint(label: 'Nov', amount: 367),
      SpendingBudgetHistoryPoint(label: 'Dec', amount: 333),
    ],
  ),
  SpendingBudgetCategory(
    id: 'personal',
    name: 'Personal care',
    icon: Icons.spa_outlined,
    accentColor: PayaboColors.headerIconAccent,
    linkedSpendingCategoryId: 'shopping',
    lineItems: <SpendingBudgetLineItem>[
      SpendingBudgetLineItem(
        id: 'grooming',
        name: 'Hair & grooming',
        allocated: 150,
        spent: 125.55,
      ),
      SpendingBudgetLineItem(
        id: 'pharmacy',
        name: 'Pharmacy',
        allocated: 120,
        spent: 52,
      ),
      SpendingBudgetLineItem(
        id: 'gym',
        name: 'Gym',
        allocated: 80,
        spent: 33,
      ),
    ],
    history: <SpendingBudgetHistoryPoint>[
      SpendingBudgetHistoryPoint(label: 'Jan', amount: 260),
      SpendingBudgetHistoryPoint(label: 'Feb', amount: 302),
      SpendingBudgetHistoryPoint(label: 'Mar', amount: 210.55, isCurrent: true),
      SpendingBudgetHistoryPoint(label: 'Apr', amount: 290),
      SpendingBudgetHistoryPoint(label: 'May', amount: 355),
      SpendingBudgetHistoryPoint(label: 'Jun', amount: 270),
      SpendingBudgetHistoryPoint(label: 'Jul', amount: 330),
      SpendingBudgetHistoryPoint(label: 'Aug', amount: 245),
      SpendingBudgetHistoryPoint(label: 'Sep', amount: 318),
      SpendingBudgetHistoryPoint(label: 'Oct', amount: 282),
      SpendingBudgetHistoryPoint(label: 'Nov', amount: 340),
      SpendingBudgetHistoryPoint(label: 'Dec', amount: 294),
    ],
  ),
];

final NumberFormat _spendingBudgetWholeCurrencyFormatter =
    NumberFormat.currency(
  locale: 'en_GB',
  symbol: '£',
  decimalDigits: 0,
);

final NumberFormat _spendingBudgetDecimalCurrencyFormatter =
    NumberFormat.currency(
  locale: 'en_GB',
  symbol: '£',
  decimalDigits: 2,
);

class SpendingBudgetSummary {
  const SpendingBudgetSummary({
    required this.monthLabel,
    required this.totalBudget,
    required this.totalSpent,
    required this.categoryCount,
  });

  factory SpendingBudgetSummary.fromCategories({
    required String monthLabel,
    required List<SpendingBudgetCategory> categories,
  }) {
    final double totalBudget = categories.fold<double>(
      0,
      (double sum, SpendingBudgetCategory category) => sum + category.allocated,
    );
    final double totalSpent = categories.fold<double>(
      0,
      (double sum, SpendingBudgetCategory category) => sum + category.spent,
    );

    return SpendingBudgetSummary(
      monthLabel: monthLabel,
      totalBudget: totalBudget,
      totalSpent: totalSpent,
      categoryCount: categories.length,
    );
  }

  final String monthLabel;
  final double totalBudget;
  final double totalSpent;
  final int categoryCount;

  double get remaining => totalBudget - totalSpent;
  double get progress => totalBudget == 0 ? 0 : totalSpent / totalBudget;

  String get statusLabel {
    if (categoryCount == 0) {
      return 'Start planning';
    }

    if (remaining < 0) {
      return 'Over plan';
    }

    if (progress >= 0.9) {
      return 'Almost there';
    }

    return 'On track';
  }

  Color get statusColor {
    if (categoryCount == 0) {
      return PayaboColors.primary;
    }

    if (remaining < 0) {
      return PayaboColors.danger;
    }

    if (progress >= 0.9) {
      return PayaboColors.warning;
    }

    return PayaboColors.success;
  }

  String get description {
    if (categoryCount == 0) {
      return 'Create your first category budget to start tracking how much is left before month end.';
    }

    return '$categoryCount active budgets covering home, food, transport, utilities, and personal care.';
  }

  String get leftToSpendLabel {
    if (remaining >= 0) {
      return formatSpendingBudgetCurrency(remaining);
    }

    return '${formatSpendingBudgetCurrency(remaining.abs())} over';
  }

  Color get leftToSpendColor {
    if (categoryCount == 0) {
      return PayaboColors.primary;
    }

    return remaining >= 0 ? PayaboColors.success : PayaboColors.danger;
  }

  String get progressLabel {
    if (totalBudget == 0) {
      return 'No monthly budget set yet.';
    }

    return '${(progress * 100).toStringAsFixed(1)}% of this month\'s plan is already used.';
  }
}

class SpendingBudgetCategory {
  const SpendingBudgetCategory({
    required this.id,
    required this.name,
    required this.icon,
    required this.accentColor,
    required this.lineItems,
    required this.history,
    this.linkedSpendingCategoryId,
  });

  final String id;
  final String name;
  final IconData icon;
  final Color accentColor;
  final String? linkedSpendingCategoryId;
  final List<SpendingBudgetLineItem> lineItems;
  final List<SpendingBudgetHistoryPoint> history;

  SpendingBudgetCategory copyWith({
    String? id,
    String? name,
    IconData? icon,
    Color? accentColor,
    String? linkedSpendingCategoryId,
    bool clearLinkedSpendingCategoryId = false,
    List<SpendingBudgetLineItem>? lineItems,
    List<SpendingBudgetHistoryPoint>? history,
  }) {
    return SpendingBudgetCategory(
      id: id ?? this.id,
      name: name ?? this.name,
      icon: icon ?? this.icon,
      accentColor: accentColor ?? this.accentColor,
      linkedSpendingCategoryId: clearLinkedSpendingCategoryId
          ? null
          : (linkedSpendingCategoryId ?? this.linkedSpendingCategoryId),
      lineItems: lineItems ?? this.lineItems,
      history: history ?? this.history,
    );
  }

  double get allocated => lineItems.fold<double>(
        0,
        (double sum, SpendingBudgetLineItem item) => sum + item.allocated,
      );

  double get spent => lineItems.fold<double>(
        0,
        (double sum, SpendingBudgetLineItem item) => sum + item.spent,
      );
}

class SpendingBudgetLineItem {
  const SpendingBudgetLineItem({
    required this.id,
    required this.name,
    required this.allocated,
    required this.spent,
  });

  final String id;
  final String name;
  final double allocated;
  final double spent;

  SpendingBudgetLineItem copyWith({
    String? id,
    String? name,
    double? allocated,
    double? spent,
  }) {
    return SpendingBudgetLineItem(
      id: id ?? this.id,
      name: name ?? this.name,
      allocated: allocated ?? this.allocated,
      spent: spent ?? this.spent,
    );
  }
}

class SpendingBudgetHistoryPoint {
  const SpendingBudgetHistoryPoint({
    required this.label,
    required this.amount,
    this.isCurrent = false,
  });

  final String label;
  final double amount;
  final bool isCurrent;
}

class SpendingBudgetState {
  const SpendingBudgetState({
    required this.progress,
    required this.progressColor,
    required this.statusLabel,
    required this.remainingLabel,
    required this.remainingColor,
    required this.percentUsedLabel,
    required this.remainingAmount,
  });

  factory SpendingBudgetState.fromBudget({
    required double allocated,
    required double spent,
  }) {
    final double remaining = allocated - spent;
    final double progress = allocated == 0 ? 0 : spent / allocated;
    final bool isOver = remaining < 0;
    final bool isClose = !isOver && progress >= 0.9;

    final Color progressColor = isOver
        ? PayaboColors.danger
        : isClose
            ? PayaboColors.warning
            : PayaboColors.primary;

    final String remainingLabel = isOver
        ? '${formatSpendingBudgetCurrency(remaining.abs())} over'
        : '${formatSpendingBudgetCurrency(remaining)} left';

    return SpendingBudgetState(
      progress: progress,
      progressColor: progressColor,
      statusLabel: isOver
          ? 'Overspent'
          : isClose
              ? 'Close'
              : 'On track',
      remainingLabel: remainingLabel,
      remainingColor: isOver ? PayaboColors.danger : PayaboColors.success,
      percentUsedLabel: '${(progress.clamp(0, 1) * 100).toStringAsFixed(0)}%',
      remainingAmount: remaining,
    );
  }

  final double progress;
  final Color progressColor;
  final String statusLabel;
  final String remainingLabel;
  final Color remainingColor;
  final String percentUsedLabel;
  final double remainingAmount;
}

SpendingBudgetCategory getSpendingBudgetCategoryById(String id) {
  for (final SpendingBudgetCategory category in spendingBudgetCategories) {
    if (category.id == id) {
      return category;
    }
  }

  return spendingBudgetCategories.first;
}

String formatSpendingBudgetCurrency(double amount) {
  final double roundedAmount = amount.roundToDouble();
  final bool isWholeAmount = (amount - roundedAmount).abs() < 0.005;

  return isWholeAmount
      ? _spendingBudgetWholeCurrencyFormatter.format(amount)
      : _spendingBudgetDecimalCurrencyFormatter.format(amount);
}

List<SpendingBudgetCategory> cloneSpendingBudgetCategories(
  List<SpendingBudgetCategory> categories,
) {
  return categories.map(cloneSpendingBudgetCategory).toList(growable: false);
}

SpendingBudgetCategory cloneSpendingBudgetCategory(
  SpendingBudgetCategory category,
) {
  return category.copyWith(
    lineItems: category.lineItems
        .map((SpendingBudgetLineItem item) => item.copyWith())
        .toList(growable: false),
    history: category.history
        .map(
          (SpendingBudgetHistoryPoint point) => SpendingBudgetHistoryPoint(
            label: point.label,
            amount: point.amount,
            isCurrent: point.isCurrent,
          ),
        )
        .toList(growable: false),
  );
}
