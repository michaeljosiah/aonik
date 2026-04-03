import 'package:flutter/material.dart';

import '../../../../shared/theme/payabo_color_resolver.dart';
import '../../../../shared/theme/payabo_radii.dart';
import '../../../../shared/theme/payabo_spacing.dart';

// ─────────────────────────────────────────────────────────
//  Category data
//
//  The canonical category codes are defined by the backend
//  (GET /personal-finance/categories). The [code] field
//  must match the backend's canonical code exactly. Display
//  names and icons here are the offline fallback; the live
//  app should hydrate from the API on startup.
// ─────────────────────────────────────────────────────────

class CategoryItem {
  const CategoryItem({
    required this.code,
    required this.name,
    required this.icon,
  });

  /// Canonical backend category code (e.g. "groceries", "eating_out").
  final String code;

  /// User-facing display name.
  final String name;

  final IconData icon;
}

/// Pre-built lookup table for O(1) category display name resolution.
final Map<String, String> _categoryDisplayNameMap = <String, String>{
  for (final CategoryItem c in defaultCategories) c.code: c.name,
};

/// Maps a backend canonical category code to its display name.
/// Falls back to title-casing the code if not found.
String categoryDisplayName(String code) {
  final String? name = _categoryDisplayNameMap[code];
  if (name != null) return name;
  // Fallback: title-case the code (e.g. "eating_out" → "Eating Out")
  return code
      .split('_')
      .map((String w) => w.isEmpty ? '' : '${w[0].toUpperCase()}${w.substring(1)}')
      .join(' ');
}

/// Maps a subcategory code to its human-readable display name.
///
/// Expects the subcategory code only (e.g. `"supermarket"`), with the parent
/// [categoryCode] provided separately. Looks up the composite key
/// `"category:subcategory"` in [_subCategoryDisplayNames]. Returns `null` if
/// the subcategory is not recognised.
String? subCategoryDisplayName(String categoryCode, String? subCategoryCode) {
  if (subCategoryCode == null || subCategoryCode.isEmpty) return null;
  return _subCategoryDisplayNames['$categoryCode:$subCategoryCode'];
}

/// Subcategory display names keyed by `"category:subcategory"`.
///
/// Mirrors the backend's `TransactionCategoryReference.SubCategoryMetadata`
/// dictionary. Kept as a static offline fallback; the live app can hydrate
/// from `GET /personal-finance/categories` at startup.
const Map<String, String> _subCategoryDisplayNames = <String, String>{
  // ── Income ──────────────────────────────────────────────
  'income:salary': 'Salary',
  'income:freelance': 'Freelance',
  'income:benefits': 'Benefits',
  'income:refund': 'Refund',
  'income:interest': 'Interest',
  'income:rental_income': 'Rental Income',
  'income:side_hustle': 'Side Hustle',

  // ── Transfer In ─────────────────────────────────────────
  'transfer_in:own_account': 'Own Account',
  'transfer_in:received_transfer': 'Received Transfer',

  // ── Transfer Out ────────────────────────────────────────
  'transfer_out:own_account': 'Own Account',
  'transfer_out:sent_transfer': 'Sent Transfer',

  // ── Family Support ──────────────────────────────────────
  'family_support:remittance': 'Remittance',
  'family_support:family_allowance': 'Family Allowance',
  'family_support:school_fees': 'School Fees',
  'family_support:medical_support': 'Medical Support',

  // ── Housing ─────────────────────────────────────────────
  'housing:rent': 'Rent',
  'housing:mortgage': 'Mortgage',
  'housing:repairs': 'Repairs & Maintenance',
  'housing:furnishing': 'Furnishing',
  'housing:property_tax': 'Property Tax',

  // ── Groceries ───────────────────────────────────────────
  'groceries:supermarket': 'Supermarket',
  'groceries:market': 'Market',
  'groceries:online_grocery': 'Online Grocery',
  'groceries:alcohol': 'Alcohol & Drinks',

  // ── Eating Out ──────────────────────────────────────────
  'eating_out:restaurant': 'Restaurant',
  'eating_out:fast_food': 'Fast Food',
  'eating_out:cafe': 'Café & Coffee',
  'eating_out:delivery': 'Food Delivery',
  'eating_out:takeaway': 'Takeaway',

  // ── Transport ───────────────────────────────────────────
  'transport:fuel': 'Fuel',
  'transport:public_transit': 'Public Transit',
  'transport:ride_hailing': 'Ride Hailing',
  'transport:parking': 'Parking',
  'transport:car_maintenance': 'Car Maintenance',
  'transport:tolls': 'Tolls',

  // ── Bills ───────────────────────────────────────────────
  'bills:electricity': 'Electricity',
  'bills:water': 'Water',
  'bills:gas': 'Gas',
  'bills:phone': 'Phone & Mobile',
  'bills:internet': 'Internet',
  'bills:insurance': 'Insurance',
  'bills:council_tax': 'Council Tax / Rates',
  'bills:waste': 'Waste & Sewage',
  'bills:tv_licence': 'TV Licence',

  // ── Health ──────────────────────────────────────────────
  'health:doctor': 'Doctor / GP',
  'health:pharmacy': 'Pharmacy',
  'health:hospital': 'Hospital',
  'health:dental': 'Dental',
  'health:optical': 'Optical',
  'health:mental_health': 'Mental Health',

  // ── Education ───────────────────────────────────────────
  'education:tuition': 'Tuition Fees',
  'education:courses': 'Courses & Training',
  'education:books': 'Books & Materials',
  'education:exams': 'Exams & Certification',

  // ── Shopping ────────────────────────────────────────────
  'shopping:clothing': 'Clothing & Accessories',
  'shopping:electronics': 'Electronics',
  'shopping:home_goods': 'Home & Garden',
  'shopping:online': 'Online Shopping',
  'shopping:department_store': 'Department Store',

  // ── Personal Care ───────────────────────────────────────
  'personal_care:haircut': 'Haircut & Barber',
  'personal_care:beauty': 'Beauty & Spa',
  'personal_care:cosmetics': 'Cosmetics',

  // ── Gifts ───────────────────────────────────────────────
  'gifts:gift_card': 'Gift Card',
  'gifts:present': 'Present',
  'gifts:flowers': 'Flowers',

  // ── Entertainment ───────────────────────────────────────
  'entertainment:cinema': 'Cinema',
  'entertainment:gaming': 'Gaming',
  'entertainment:events': 'Events & Concerts',
  'entertainment:gambling': 'Gambling & Betting',

  // ── Subscriptions ───────────────────────────────────────
  'subscriptions:streaming': 'Streaming',
  'subscriptions:music': 'Music',
  'subscriptions:software': 'Software',
  'subscriptions:news': 'News & Magazines',
  'subscriptions:cloud_storage': 'Cloud Storage',

  // ── Travel ──────────────────────────────────────────────
  'travel:flights': 'Flights',
  'travel:hotel': 'Hotel & Accommodation',
  'travel:car_rental': 'Car Rental',
  'travel:booking': 'Travel Booking',

  // ── Fitness ─────────────────────────────────────────────
  'fitness:gym': 'Gym Membership',
  'fitness:sports': 'Sports & Activities',
  'fitness:equipment': 'Equipment',

  // ── Pets ────────────────────────────────────────────────
  'pets:food': 'Pet Food',
  'pets:vet': 'Vet',
  'pets:supplies': 'Pet Supplies',

  // ── Savings ─────────────────────────────────────────────
  'savings:emergency_fund': 'Emergency Fund',
  'savings:goal_savings': 'Goal Savings',
  'savings:fixed_deposit': 'Fixed Deposit',

  // ── Investments ─────────────────────────────────────────
  'investments:stocks': 'Stocks & Shares',
  'investments:crypto': 'Crypto',
  'investments:funds': 'Funds & ISA',
  'investments:pension': 'Pension',

  // ── Loan Payments ───────────────────────────────────────
  'loan_payments:personal_loan': 'Personal Loan',
  'loan_payments:bnpl': 'Buy Now Pay Later',
  'loan_payments:credit_card': 'Credit Card',
  'loan_payments:student_loan': 'Student Loan',

  // ── Bank Fees ───────────────────────────────────────────
  'bank_fees:overdraft': 'Overdraft Fee',
  'bank_fees:atm': 'ATM Fee',
  'bank_fees:card_fee': 'Card Fee',
  'bank_fees:foreign_tx': 'Foreign Transaction Fee',
  'bank_fees:sms_alert': 'SMS Alert Fee',

  // ── Charity ─────────────────────────────────────────────
  'charity:donation': 'Donation',
  'charity:religious': 'Religious Giving',
  'charity:crowdfunding': 'Crowdfunding',
};

/// Pre-built lookup table for O(1) category icon resolution.
final Map<String, IconData> _categoryIconMap = <String, IconData>{
  for (final CategoryItem c in defaultCategories) c.code: c.icon,
};

/// Maps a backend canonical category code to an icon.
/// Falls back to a generic category icon if not found.
IconData categoryIcon(String code) {
  return _categoryIconMap[code] ?? Icons.category_outlined;
}

/// The 26 canonical categories aligned with the backend taxonomy.
/// Sorted by group and sort order to match the API response.
const List<CategoryItem> defaultCategories = <CategoryItem>[
  // Income
  CategoryItem(code: 'income', name: 'Income', icon: Icons.account_balance_wallet_outlined),

  // Transfers
  CategoryItem(code: 'transfer_in', name: 'Transfer In', icon: Icons.call_received_outlined),
  CategoryItem(code: 'transfer_out', name: 'Transfer Out', icon: Icons.call_made_outlined),
  CategoryItem(code: 'family_support', name: 'Family Support', icon: Icons.family_restroom_outlined),

  // Essentials
  CategoryItem(code: 'housing', name: 'Housing', icon: Icons.home_outlined),
  CategoryItem(code: 'groceries', name: 'Groceries', icon: Icons.shopping_cart_outlined),
  CategoryItem(code: 'eating_out', name: 'Eating Out', icon: Icons.restaurant_outlined),
  CategoryItem(code: 'transport', name: 'Transport', icon: Icons.directions_car_outlined),
  CategoryItem(code: 'bills', name: 'Bills', icon: Icons.receipt_long_outlined),
  CategoryItem(code: 'health', name: 'Health', icon: Icons.favorite_outline),
  CategoryItem(code: 'education', name: 'Education', icon: Icons.school_outlined),

  // Shopping
  CategoryItem(code: 'shopping', name: 'Shopping', icon: Icons.shopping_bag_outlined),
  CategoryItem(code: 'personal_care', name: 'Personal Care', icon: Icons.spa_outlined),
  CategoryItem(code: 'gifts', name: 'Gifts', icon: Icons.card_giftcard_outlined),

  // Lifestyle
  CategoryItem(code: 'entertainment', name: 'Entertainment', icon: Icons.movie_outlined),
  CategoryItem(code: 'subscriptions', name: 'Subscriptions', icon: Icons.subscriptions_outlined),
  CategoryItem(code: 'travel', name: 'Travel', icon: Icons.flight_outlined),
  CategoryItem(code: 'fitness', name: 'Fitness', icon: Icons.fitness_center_outlined),
  CategoryItem(code: 'pets', name: 'Pets', icon: Icons.pets_outlined),

  // Financial
  CategoryItem(code: 'savings', name: 'Savings', icon: Icons.savings_outlined),
  CategoryItem(code: 'investments', name: 'Investments', icon: Icons.trending_up_outlined),
  CategoryItem(code: 'loan_payments', name: 'Loan Payments', icon: Icons.money_off_outlined),
  CategoryItem(code: 'bank_fees', name: 'Bank Fees', icon: Icons.account_balance_outlined),

  // Services
  CategoryItem(code: 'charity', name: 'Charity', icon: Icons.volunteer_activism_outlined),

  // Other
  CategoryItem(code: 'other', name: 'Other', icon: Icons.more_horiz_outlined),
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
                          final bool isSelected =
                              category.code == widget.currentCategory;

                          return _CategoryGridItem(
                            category: category,
                            isSelected: isSelected,
                            onTap: () {
                              Navigator.of(context).pop(category.code);
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
                      // Derive a snake_case code from the user-typed name
                      // so the sheet consistently returns codes, not
                      // display names.
                      final String code = name
                          .toLowerCase()
                          .replaceAll(RegExp(r'[^a-z0-9\s]'), '')
                          .trim()
                          .replaceAll(RegExp(r'\s+'), '_');
                      // Close the dialog first, then pop the sheet
                      // on the next frame to avoid concurrent rebuild
                      // + navigation causing semantics assertion.
                      Navigator.of(dialogContext).pop();
                      WidgetsBinding.instance.addPostFrameCallback((_) {
                        if (mounted) {
                          Navigator.of(this.context).pop(code);
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
