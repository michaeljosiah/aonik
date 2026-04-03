import 'package:flutter/material.dart';
import 'package:intl/intl.dart';

import '../../../shared/theme/payabo_color_resolver.dart';

String get spendingBudgetMonthLabel =>
    DateFormat('MMMM yyyy').format(DateTime.now());

enum SpendingBudgetColorRole {
  primary,
  success,
  warning,
  danger,
  info,
  accent,
}

extension SpendingBudgetColorRoleX on SpendingBudgetColorRole {
  Color resolve(PayaboColorResolver colors) {
    switch (this) {
      case SpendingBudgetColorRole.primary:
        return colors.primary;
      case SpendingBudgetColorRole.success:
        return colors.success;
      case SpendingBudgetColorRole.warning:
        return colors.warning;
      case SpendingBudgetColorRole.danger:
        return colors.danger;
      case SpendingBudgetColorRole.info:
        return colors.info;
      case SpendingBudgetColorRole.accent:
        return colors.headerIconAccent;
    }
  }
}

/// The original 5 categories used to seed the populated demo dashboard.
/// Keep this small so the demo is not overwhelming.
const List<SpendingBudgetCategory> spendingBudgetCategories =
    <SpendingBudgetCategory>[
  SpendingBudgetCategory(
    id: 'housing',
    name: 'Housing',
    description: 'Track rent, repairs, and household supplies.',
    icon: Icons.home_work_outlined,
    accentRole: SpendingBudgetColorRole.primary,
    linkedSpendingCategoryId: 'finances',
    lineItems: <SpendingBudgetLineItem>[
      SpendingBudgetLineItem(
        id: 'housing-budget',
        name: 'Budget',
        allocated: 1200,
        spent: 950,
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
    description: 'Supermarket runs, fresh market, and coffee stops.',
    icon: Icons.local_grocery_store_outlined,
    accentRole: SpendingBudgetColorRole.success,
    linkedSpendingCategoryId: 'groceries',
    lineItems: <SpendingBudgetLineItem>[
      SpendingBudgetLineItem(
        id: 'groceries-budget',
        name: 'Budget',
        allocated: 600,
        spent: 418.65,
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
    description: 'Fuel, ride apps, and public transit fares.',
    icon: Icons.directions_bus_outlined,
    accentRole: SpendingBudgetColorRole.warning,
    linkedSpendingCategoryId: 'transport',
    lineItems: <SpendingBudgetLineItem>[
      SpendingBudgetLineItem(
        id: 'transport-budget',
        name: 'Budget',
        allocated: 420,
        spent: 448.2,
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
    description: 'Electricity, water, and internet bills.',
    icon: Icons.lightbulb_outline_rounded,
    accentRole: SpendingBudgetColorRole.info,
    linkedSpendingCategoryId: 'finances',
    lineItems: <SpendingBudgetLineItem>[
      SpendingBudgetLineItem(
        id: 'utilities-budget',
        name: 'Budget',
        allocated: 380,
        spent: 241.3,
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
    description: 'Grooming, pharmacy, and gym memberships.',
    icon: Icons.spa_outlined,
    accentRole: SpendingBudgetColorRole.accent,
    linkedSpendingCategoryId: 'shopping',
    lineItems: <SpendingBudgetLineItem>[
      SpendingBudgetLineItem(
        id: 'personal-budget',
        name: 'Budget',
        allocated: 350,
        spent: 210.55,
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

/// All budget categories available for creation.
///
/// Includes the original 5 populated-demo categories plus 13 additional
/// categories that match the spending-category grid in the app. The
/// budget-creation picker reads from this list so users can create a budget
/// for any recognised spending category.
///
/// Categories are created with **empty line items** — the user adds their own
/// sub-budget lines after creation.
const List<SpendingBudgetCategory> allBudgetCategoryTemplates =
    <SpendingBudgetCategory>[
  // ── Original 5 (also in spendingBudgetCategories) ──────────────────
  SpendingBudgetCategory(
    id: 'housing',
    name: 'Housing',
    description: 'Track rent, repairs, and household supplies.',
    icon: Icons.home_work_outlined,
    accentRole: SpendingBudgetColorRole.primary,
    linkedSpendingCategoryId: 'finances',
    lineItems: <SpendingBudgetLineItem>[],
    history: _emptyHistory,
  ),
  SpendingBudgetCategory(
    id: 'groceries',
    name: 'Food & Groceries',
    description: 'Supermarket runs, fresh market, and coffee stops.',
    icon: Icons.local_grocery_store_outlined,
    accentRole: SpendingBudgetColorRole.success,
    linkedSpendingCategoryId: 'groceries',
    lineItems: <SpendingBudgetLineItem>[],
    history: _emptyHistory,
  ),
  SpendingBudgetCategory(
    id: 'transport',
    name: 'Transport',
    description: 'Fuel, ride apps, and public transit fares.',
    icon: Icons.directions_bus_outlined,
    accentRole: SpendingBudgetColorRole.warning,
    linkedSpendingCategoryId: 'transport',
    lineItems: <SpendingBudgetLineItem>[],
    history: _emptyHistory,
  ),
  SpendingBudgetCategory(
    id: 'utilities',
    name: 'Utilities',
    description: 'Electricity, water, and internet bills.',
    icon: Icons.lightbulb_outline_rounded,
    accentRole: SpendingBudgetColorRole.info,
    linkedSpendingCategoryId: 'finances',
    lineItems: <SpendingBudgetLineItem>[],
    history: _emptyHistory,
  ),
  SpendingBudgetCategory(
    id: 'personal',
    name: 'Personal Care',
    description: 'Grooming, pharmacy, and gym memberships.',
    icon: Icons.spa_outlined,
    accentRole: SpendingBudgetColorRole.accent,
    linkedSpendingCategoryId: 'shopping',
    lineItems: <SpendingBudgetLineItem>[],
    history: _emptyHistory,
  ),

  // ── Additional categories ──────────────────────────────────────────
  SpendingBudgetCategory(
    id: 'eating-out',
    name: 'Eating Out',
    description: 'Restaurants, takeaways, and dining with friends.',
    icon: Icons.restaurant_outlined,
    accentRole: SpendingBudgetColorRole.warning,
    linkedSpendingCategoryId: 'groceries',
    lineItems: <SpendingBudgetLineItem>[],
    history: _emptyHistory,
  ),
  SpendingBudgetCategory(
    id: 'shopping',
    name: 'Shopping',
    description: 'Clothing, electronics, and everyday purchases.',
    icon: Icons.shopping_bag_outlined,
    accentRole: SpendingBudgetColorRole.accent,
    linkedSpendingCategoryId: 'shopping',
    lineItems: <SpendingBudgetLineItem>[],
    history: _emptyHistory,
  ),
  SpendingBudgetCategory(
    id: 'entertainment',
    name: 'Entertainment',
    description: 'Movies, streaming, games, and nights out.',
    icon: Icons.movie_outlined,
    accentRole: SpendingBudgetColorRole.primary,
    linkedSpendingCategoryId: 'entertainment',
    lineItems: <SpendingBudgetLineItem>[],
    history: _emptyHistory,
  ),
  SpendingBudgetCategory(
    id: 'bills',
    name: 'Bills',
    description: 'Phone, insurance, and recurring monthly bills.',
    icon: Icons.receipt_long_outlined,
    accentRole: SpendingBudgetColorRole.danger,
    linkedSpendingCategoryId: 'finances',
    lineItems: <SpendingBudgetLineItem>[],
    history: _emptyHistory,
  ),
  SpendingBudgetCategory(
    id: 'health',
    name: 'Health',
    description: 'Doctor visits, prescriptions, and wellness.',
    icon: Icons.favorite_outline,
    accentRole: SpendingBudgetColorRole.danger,
    linkedSpendingCategoryId: 'shopping',
    lineItems: <SpendingBudgetLineItem>[],
    history: _emptyHistory,
  ),
  SpendingBudgetCategory(
    id: 'education',
    name: 'Education',
    description: 'Tuition, books, courses, and learning materials.',
    icon: Icons.school_outlined,
    accentRole: SpendingBudgetColorRole.info,
    linkedSpendingCategoryId: 'finances',
    lineItems: <SpendingBudgetLineItem>[],
    history: _emptyHistory,
  ),
  SpendingBudgetCategory(
    id: 'gifts',
    name: 'Gifts',
    description: 'Birthday, holiday, and special occasion presents.',
    icon: Icons.card_giftcard_outlined,
    accentRole: SpendingBudgetColorRole.accent,
    linkedSpendingCategoryId: 'shopping',
    lineItems: <SpendingBudgetLineItem>[],
    history: _emptyHistory,
  ),
  SpendingBudgetCategory(
    id: 'travel',
    name: 'Travel',
    description: 'Flights, hotels, and holiday spending.',
    icon: Icons.flight_outlined,
    accentRole: SpendingBudgetColorRole.primary,
    linkedSpendingCategoryId: 'transport',
    lineItems: <SpendingBudgetLineItem>[],
    history: _emptyHistory,
  ),
  SpendingBudgetCategory(
    id: 'savings',
    name: 'Savings',
    description: 'Emergency fund, rainy day, and saving goals.',
    icon: Icons.savings_outlined,
    accentRole: SpendingBudgetColorRole.success,
    linkedSpendingCategoryId: 'finances',
    lineItems: <SpendingBudgetLineItem>[],
    history: _emptyHistory,
  ),
  SpendingBudgetCategory(
    id: 'subscriptions',
    name: 'Subscriptions',
    description: 'Streaming, apps, memberships, and recurring charges.',
    icon: Icons.subscriptions_outlined,
    accentRole: SpendingBudgetColorRole.info,
    linkedSpendingCategoryId: 'entertainment',
    lineItems: <SpendingBudgetLineItem>[],
    history: _emptyHistory,
  ),
  SpendingBudgetCategory(
    id: 'charity',
    name: 'Charity',
    description: 'Donations, tithes, and community giving.',
    icon: Icons.volunteer_activism_outlined,
    accentRole: SpendingBudgetColorRole.success,
    linkedSpendingCategoryId: 'finances',
    lineItems: <SpendingBudgetLineItem>[],
    history: _emptyHistory,
  ),
  SpendingBudgetCategory(
    id: 'fitness',
    name: 'Fitness',
    description: 'Gym, sports gear, and workout classes.',
    icon: Icons.fitness_center_outlined,
    accentRole: SpendingBudgetColorRole.warning,
    linkedSpendingCategoryId: 'shopping',
    lineItems: <SpendingBudgetLineItem>[],
    history: _emptyHistory,
  ),
  SpendingBudgetCategory(
    id: 'pets',
    name: 'Pets',
    description: 'Food, vet visits, and supplies for your pets.',
    icon: Icons.pets_outlined,
    accentRole: SpendingBudgetColorRole.accent,
    linkedSpendingCategoryId: 'shopping',
    lineItems: <SpendingBudgetLineItem>[],
    history: _emptyHistory,
  ),
  SpendingBudgetCategory(
    id: 'investments',
    name: 'Investments',
    description: 'Stocks, crypto, and other investment contributions.',
    icon: Icons.trending_up_outlined,
    accentRole: SpendingBudgetColorRole.success,
    linkedSpendingCategoryId: 'finances',
    lineItems: <SpendingBudgetLineItem>[],
    history: _emptyHistory,
  ),
];

/// Shared empty 12-month history used by creation templates (no prior data).
const List<SpendingBudgetHistoryPoint> _emptyHistory =
    <SpendingBudgetHistoryPoint>[
  SpendingBudgetHistoryPoint(label: 'Jan', amount: 0),
  SpendingBudgetHistoryPoint(label: 'Feb', amount: 0),
  SpendingBudgetHistoryPoint(label: 'Mar', amount: 0, isCurrent: true),
  SpendingBudgetHistoryPoint(label: 'Apr', amount: 0),
  SpendingBudgetHistoryPoint(label: 'May', amount: 0),
  SpendingBudgetHistoryPoint(label: 'Jun', amount: 0),
  SpendingBudgetHistoryPoint(label: 'Jul', amount: 0),
  SpendingBudgetHistoryPoint(label: 'Aug', amount: 0),
  SpendingBudgetHistoryPoint(label: 'Sep', amount: 0),
  SpendingBudgetHistoryPoint(label: 'Oct', amount: 0),
  SpendingBudgetHistoryPoint(label: 'Nov', amount: 0),
  SpendingBudgetHistoryPoint(label: 'Dec', amount: 0),
];


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

  SpendingBudgetColorRole get statusColorRole {
    if (categoryCount == 0) {
      return SpendingBudgetColorRole.primary;
    }

    if (remaining < 0) {
      return SpendingBudgetColorRole.danger;
    }

    if (progress >= 0.9) {
      return SpendingBudgetColorRole.warning;
    }

    return SpendingBudgetColorRole.success;
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

  SpendingBudgetColorRole get leftToSpendColorRole {
    if (categoryCount == 0) {
      return SpendingBudgetColorRole.primary;
    }

    return remaining >= 0
        ? SpendingBudgetColorRole.success
        : SpendingBudgetColorRole.danger;
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
    required this.accentRole,
    required this.lineItems,
    required this.history,
    this.description,
    this.linkedSpendingCategoryId,
  });

  final String id;
  final String name;
  final String? description;
  final IconData icon;
  final SpendingBudgetColorRole accentRole;
  final String? linkedSpendingCategoryId;
  final List<SpendingBudgetLineItem> lineItems;
  final List<SpendingBudgetHistoryPoint> history;

  SpendingBudgetCategory copyWith({
    String? id,
    String? name,
    String? description,
    IconData? icon,
    SpendingBudgetColorRole? accentRole,
    String? linkedSpendingCategoryId,
    bool clearLinkedSpendingCategoryId = false,
    List<SpendingBudgetLineItem>? lineItems,
    List<SpendingBudgetHistoryPoint>? history,
  }) {
    return SpendingBudgetCategory(
      id: id ?? this.id,
      name: name ?? this.name,
      description: description ?? this.description,
      icon: icon ?? this.icon,
      accentRole: accentRole ?? this.accentRole,
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
    required this.progressColorRole,
    required this.statusLabel,
    required this.remainingLabel,
    required this.remainingColorRole,
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

    final SpendingBudgetColorRole progressColorRole = isOver
        ? SpendingBudgetColorRole.danger
        : isClose
            ? SpendingBudgetColorRole.warning
            : SpendingBudgetColorRole.primary;

    final String remainingLabel = isOver
        ? '${formatSpendingBudgetCurrency(remaining.abs())} over'
        : '${formatSpendingBudgetCurrency(remaining)} left';

    return SpendingBudgetState(
      progress: progress,
      progressColorRole: progressColorRole,
      statusLabel: isOver
          ? 'Overspent'
          : isClose
              ? 'Close'
              : 'On track',
      remainingLabel: remainingLabel,
      remainingColorRole: isOver
          ? SpendingBudgetColorRole.danger
          : SpendingBudgetColorRole.success,
      percentUsedLabel: '${(progress.clamp(0, 1) * 100).toStringAsFixed(0)}%',
      remainingAmount: remaining,
    );
  }

  final double progress;
  final SpendingBudgetColorRole progressColorRole;
  final String statusLabel;
  final String remainingLabel;
  final SpendingBudgetColorRole remainingColorRole;
  final String percentUsedLabel;
  final double remainingAmount;
}

SpendingBudgetCategory getSpendingBudgetCategoryById(String id) {
  for (final SpendingBudgetCategory category in allBudgetCategoryTemplates) {
    if (category.id == id) {
      return category;
    }
  }

  return allBudgetCategoryTemplates.first;
}

String formatSpendingBudgetCurrency(double amount) {
  final double roundedAmount = amount.roundToDouble();
  final bool isWholeAmount = (amount - roundedAmount).abs() < 0.005;

  final formatter = NumberFormat.currency(
    locale: 'en_GB',
    symbol: '\u00a3',
    decimalDigits: isWholeAmount ? 0 : 2,
  );

  return formatter.format(amount);
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

SpendingBudgetCategory createStarterSpendingBudgetCategory({
  required int index,
}) {
  final String suffix = index <= 1 ? '' : ' $index';

  return SpendingBudgetCategory(
    id: 'starter-budget-$index',
    name: 'My budget$suffix',
    icon: Icons.savings_outlined,
    accentRole: SpendingBudgetColorRole.primary,
    lineItems: const <SpendingBudgetLineItem>[],
    history: const <SpendingBudgetHistoryPoint>[
      SpendingBudgetHistoryPoint(label: 'Jan', amount: 0),
      SpendingBudgetHistoryPoint(label: 'Feb', amount: 0),
      SpendingBudgetHistoryPoint(label: 'Mar', amount: 0, isCurrent: true),
      SpendingBudgetHistoryPoint(label: 'Apr', amount: 0),
      SpendingBudgetHistoryPoint(label: 'May', amount: 0),
      SpendingBudgetHistoryPoint(label: 'Jun', amount: 0),
      SpendingBudgetHistoryPoint(label: 'Jul', amount: 0),
      SpendingBudgetHistoryPoint(label: 'Aug', amount: 0),
      SpendingBudgetHistoryPoint(label: 'Sep', amount: 0),
      SpendingBudgetHistoryPoint(label: 'Oct', amount: 0),
      SpendingBudgetHistoryPoint(label: 'Nov', amount: 0),
      SpendingBudgetHistoryPoint(label: 'Dec', amount: 0),
    ],
  );
}
