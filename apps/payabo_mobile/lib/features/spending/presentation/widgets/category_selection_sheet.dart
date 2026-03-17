import 'package:flutter/material.dart';

import '../../../../shared/theme/payabo_color_resolver.dart';
import '../../../../shared/theme/payabo_radii.dart';
import '../../../../shared/theme/payabo_spacing.dart';

// ─────────────────────────────────────────────────────────
//  Category data
// ─────────────────────────────────────────────────────────

class CategoryItem {
  const CategoryItem({
    required this.name,
    required this.icon,
  });

  final String name;
  final IconData icon;
}

const List<CategoryItem> defaultCategories = <CategoryItem>[
  CategoryItem(name: 'Housing', icon: Icons.home_outlined),
  CategoryItem(name: 'Groceries', icon: Icons.shopping_cart_outlined),
  CategoryItem(name: 'Eating Out', icon: Icons.restaurant_outlined),
  CategoryItem(name: 'Transport', icon: Icons.directions_car_outlined),
  CategoryItem(name: 'Shopping', icon: Icons.shopping_bag_outlined),
  CategoryItem(name: 'Entertainment', icon: Icons.movie_outlined),
  CategoryItem(name: 'Bills', icon: Icons.receipt_long_outlined),
  CategoryItem(name: 'Health', icon: Icons.favorite_outline),
  CategoryItem(name: 'Education', icon: Icons.school_outlined),
  CategoryItem(name: 'Personal Care', icon: Icons.spa_outlined),
  CategoryItem(name: 'Gifts', icon: Icons.card_giftcard_outlined),
  CategoryItem(name: 'Travel', icon: Icons.flight_outlined),
  CategoryItem(name: 'Savings', icon: Icons.savings_outlined),
  CategoryItem(name: 'Subscriptions', icon: Icons.subscriptions_outlined),
  CategoryItem(name: 'Charity', icon: Icons.volunteer_activism_outlined),
  CategoryItem(name: 'Fitness', icon: Icons.fitness_center_outlined),
  CategoryItem(name: 'Pets', icon: Icons.pets_outlined),
  CategoryItem(name: 'Investments', icon: Icons.trending_up_outlined),
];

// ─────────────────────────────────────────────────────────
//  Show category selection sheet
// ─────────────────────────────────────────────────────────

Future<String?> showCategorySelectionSheet({
  required BuildContext context,
  required String currentCategory,
}) {
  return showModalBottomSheet<String>(
    context: context,
    isScrollControlled: true,
    isDismissible: true,
    enableDrag: true,
    backgroundColor: Colors.transparent,
    builder: (BuildContext context) {
      return _CategorySelectionSheet(
        currentCategory: currentCategory,
      );
    },
  );
}

// ─────────────────────────────────────────────────────────
//  Category selection sheet widget
// ─────────────────────────────────────────────────────────

class _CategorySelectionSheet extends StatefulWidget {
  const _CategorySelectionSheet({required this.currentCategory});

  final String currentCategory;

  @override
  State<_CategorySelectionSheet> createState() =>
      _CategorySelectionSheetState();
}

class _CategorySelectionSheetState extends State<_CategorySelectionSheet> {
  late List<CategoryItem> _categories;

  @override
  void initState() {
    super.initState();
    _categories = List<CategoryItem>.from(defaultCategories);
  }

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    final textTheme = Theme.of(context).textTheme;
    final bottomInset = MediaQuery.viewInsetsOf(context).bottom;

    return Padding(
      padding: EdgeInsets.only(bottom: bottomInset),
      child: Container(
        constraints: BoxConstraints(
          maxHeight: MediaQuery.of(context).size.height * 0.7,
        ),
        decoration: BoxDecoration(
          color: c.surfaceBase,
          borderRadius: PayaboRadii.sheetTop,
        ),
        child: SafeArea(
          top: false,
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: <Widget>[
              // ── Drag handle ──────────────────────────
              const SizedBox(height: PayaboSpacing.md),
              Center(
                child: Container(
                  width: 42,
                  height: 5,
                  decoration: BoxDecoration(
                    color: c.borderStrong,
                    borderRadius: BorderRadius.circular(999),
                  ),
                ),
              ),
              const SizedBox(height: PayaboSpacing.lg),

              // ── Header ───────────────────────────────
              Padding(
                padding: const EdgeInsets.symmetric(
                  horizontal: PayaboSpacing.xl,
                ),
                child: Row(
                  children: <Widget>[
                    Expanded(
                      child: Text(
                        'Select Category',
                        style: textTheme.titleLarge?.copyWith(
                          color: c.accentBrown,
                          fontWeight: FontWeight.w700,
                        ),
                      ),
                    ),
                    IconButton(
                      onPressed: () => Navigator.of(context).pop(),
                      icon: Icon(Icons.close, color: c.primary),
                    ),
                  ],
                ),
              ),

              const SizedBox(height: PayaboSpacing.lg),

              // ── Category grid ────────────────────────
              Flexible(
                child: SingleChildScrollView(
                  padding: const EdgeInsets.symmetric(
                    horizontal: PayaboSpacing.xl,
                  ),
                  child: Column(
                    mainAxisSize: MainAxisSize.min,
                    children: <Widget>[
                      GridView.builder(
                        shrinkWrap: true,
                        physics: const NeverScrollableScrollPhysics(),
                        gridDelegate:
                            const SliverGridDelegateWithFixedCrossAxisCount(
                          crossAxisCount: 4,
                          mainAxisSpacing: PayaboSpacing.lg,
                          crossAxisSpacing: PayaboSpacing.md,
                          childAspectRatio: 0.75,
                        ),
                        itemCount: _categories.length,
                        itemBuilder: (BuildContext context, int index) {
                          final CategoryItem category = _categories[index];
                          final bool isSelected = category.name.toLowerCase() ==
                              widget.currentCategory.toLowerCase();

                          return _CategoryGridItem(
                            category: category,
                            isSelected: isSelected,
                            onTap: () {
                              Navigator.of(context).pop(category.name);
                            },
                          );
                        },
                      ),

                      const SizedBox(height: PayaboSpacing.x2),

                      // ── Create new category button ───────
                      SizedBox(
                        width: double.infinity,
                        height: 50,
                        child: OutlinedButton.icon(
                          onPressed: () => _showCreateCategoryDialog(context),
                          icon: const Icon(Icons.add),
                          label: const Text('Create new category'),
                          style: OutlinedButton.styleFrom(
                            foregroundColor: c.primary,
                            side: BorderSide(color: c.borderWarm),
                            shape: RoundedRectangleBorder(
                              borderRadius: BorderRadius.circular(18),
                            ),
                            textStyle: textTheme.titleSmall?.copyWith(
                              fontWeight: FontWeight.w700,
                            ),
                          ),
                        ),
                      ),

                      const SizedBox(height: PayaboSpacing.xl),
                    ],
                  ),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }

  void _showCreateCategoryDialog(BuildContext context) {
    final c = context.colors;
    final textTheme = Theme.of(context).textTheme;
    final TextEditingController controller = TextEditingController();
    IconData selectedIcon = Icons.category_outlined;

    showDialog<void>(
      context: context,
      builder: (BuildContext dialogContext) {
        return StatefulBuilder(
          builder: (BuildContext context, StateSetter setDialogState) {
            return AlertDialog(
              backgroundColor: c.surfaceBase,
              shape: RoundedRectangleBorder(
                borderRadius: BorderRadius.circular(20),
              ),
              title: Text(
                'New Category',
                style: textTheme.titleLarge?.copyWith(
                  color: c.accentBrown,
                  fontWeight: FontWeight.w700,
                ),
              ),
              // Wrap content in a fixed-width SizedBox so that
              // AlertDialog's internal IntrinsicWidth never queries
              // the GridView (viewports can't return intrinsic sizes).
              content: SizedBox(
                width: 280,
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: <Widget>[
                    TextField(
                      controller: controller,
                      decoration: InputDecoration(
                        hintText: 'Category name',
                        hintStyle: textTheme.bodyMedium?.copyWith(
                          color: c.muted,
                        ),
                        border: OutlineInputBorder(
                          borderRadius: BorderRadius.circular(12),
                          borderSide: BorderSide(color: c.borderWarm),
                        ),
                        focusedBorder: OutlineInputBorder(
                          borderRadius: BorderRadius.circular(12),
                          borderSide: BorderSide(color: c.primary),
                        ),
                        contentPadding: const EdgeInsets.symmetric(
                          horizontal: PayaboSpacing.lg,
                          vertical: PayaboSpacing.md,
                        ),
                      ),
                    ),
                    const SizedBox(height: PayaboSpacing.lg),

                    Text(
                      'Choose an icon',
                      style: textTheme.titleSmall?.copyWith(
                        color: c.accentBrown,
                        fontWeight: FontWeight.w600,
                      ),
                    ),
                    const SizedBox(height: PayaboSpacing.md),

                    // Icon picker grid — height-constrained so it
                    // scrolls within the fixed-width container.
                    SizedBox(
                      height: 180,
                      child: GridView.builder(
                        gridDelegate:
                            const SliverGridDelegateWithFixedCrossAxisCount(
                          crossAxisCount: 5,
                          mainAxisSpacing: PayaboSpacing.sm,
                          crossAxisSpacing: PayaboSpacing.sm,
                        ),
                        itemCount: _availableIcons.length,
                        itemBuilder: (BuildContext context, int index) {
                          final IconData iconData = _availableIcons[index];
                          final bool isSelected = iconData == selectedIcon;

                          return GestureDetector(
                            onTap: () {
                              setDialogState(
                                () => selectedIcon = iconData,
                              );
                            },
                            child: Container(
                              decoration: BoxDecoration(
                                color: isSelected
                                    ? c.primary.withValues(alpha: 0.12)
                                    : c.surfaceMuted,
                                borderRadius: BorderRadius.circular(10),
                                border: isSelected
                                    ? Border.all(color: c.primary, width: 2)
                                    : null,
                              ),
                              child: Icon(
                                iconData,
                                size: 22,
                                color:
                                    isSelected ? c.primary : c.accentBrown,
                              ),
                            ),
                          );
                        },
                      ),
                    ),
                  ],
                ),
              ),
              actions: <Widget>[
                TextButton(
                  onPressed: () => Navigator.of(dialogContext).pop(),
                  child: Text(
                    'Cancel',
                    style: textTheme.titleSmall?.copyWith(
                      color: c.muted,
                    ),
                  ),
                ),
                FilledButton(
                  onPressed: () {
                    final String name = controller.text.trim();
                    if (name.isNotEmpty) {
                      // Close the dialog first, then pop the sheet
                      // on the next frame to avoid concurrent rebuild
                      // + navigation causing semantics assertion.
                      Navigator.of(dialogContext).pop();
                      WidgetsBinding.instance.addPostFrameCallback((_) {
                        if (mounted) {
                          Navigator.of(this.context).pop(name);
                        }
                      });
                    }
                  },
                  style: FilledButton.styleFrom(
                    backgroundColor: c.primary,
                    foregroundColor: c.surfaceBase,
                    shape: RoundedRectangleBorder(
                      borderRadius: BorderRadius.circular(12),
                    ),
                  ),
                  child: const Text('Create'),
                ),
              ],
            );
          },
        );
      },
    );
  }
}

// ─────────────────────────────────────────────────────────
//  Category grid item
// ─────────────────────────────────────────────────────────

class _CategoryGridItem extends StatelessWidget {
  const _CategoryGridItem({
    required this.category,
    required this.isSelected,
    required this.onTap,
  });

  final CategoryItem category;
  final bool isSelected;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    final textTheme = Theme.of(context).textTheme;

    return GestureDetector(
      onTap: onTap,
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: <Widget>[
          Container(
            width: 56,
            height: 56,
            decoration: BoxDecoration(
              color: isSelected
                  ? c.primary.withValues(alpha: 0.14)
                  : c.surfaceWarmAccent,
              shape: BoxShape.circle,
              border: isSelected
                  ? Border.all(color: c.primary, width: 2.5)
                  : null,
            ),
            child: Icon(
              category.icon,
              size: 24,
              color: isSelected ? c.primary : c.accentBrown,
            ),
          ),
          const SizedBox(height: PayaboSpacing.xs),
          Text(
            category.name,
            textAlign: TextAlign.center,
            maxLines: 2,
            overflow: TextOverflow.ellipsis,
            style: textTheme.bodySmall?.copyWith(
              color: isSelected ? c.primary : c.accentBrown,
              fontWeight: isSelected ? FontWeight.w700 : FontWeight.w500,
              fontSize: 10,
            ),
          ),
        ],
      ),
    );
  }
}

// ─────────────────────────────────────────────────────────
//  Available icons for new category creation
// ─────────────────────────────────────────────────────────

const List<IconData> _availableIcons = <IconData>[
  Icons.home_outlined,
  Icons.shopping_cart_outlined,
  Icons.restaurant_outlined,
  Icons.directions_car_outlined,
  Icons.shopping_bag_outlined,
  Icons.movie_outlined,
  Icons.receipt_long_outlined,
  Icons.favorite_outline,
  Icons.school_outlined,
  Icons.spa_outlined,
  Icons.card_giftcard_outlined,
  Icons.flight_outlined,
  Icons.savings_outlined,
  Icons.subscriptions_outlined,
  Icons.volunteer_activism_outlined,
  Icons.fitness_center_outlined,
  Icons.pets_outlined,
  Icons.trending_up_outlined,
  Icons.child_care_outlined,
  Icons.local_cafe_outlined,
  Icons.local_bar_outlined,
  Icons.build_outlined,
  Icons.phone_android_outlined,
  Icons.sports_esports_outlined,
  Icons.music_note_outlined,
  Icons.camera_alt_outlined,
  Icons.directions_bike_outlined,
  Icons.local_hospital_outlined,
  Icons.local_parking_outlined,
  Icons.wifi_outlined,
];
