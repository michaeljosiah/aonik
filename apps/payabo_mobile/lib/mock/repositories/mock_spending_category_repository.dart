import '../../app/demo/demo_data_mode.dart';
import '../../data/repositories/spending_category_repository.dart';
import '../mock_behavior.dart';

class MockSpendingCategoryRepository implements SpendingCategoryRepository {
  MockSpendingCategoryRepository({
    this.demoDataMode = DemoDataMode.populated,
  });

  final DemoDataMode demoDataMode;

  static const List<List<double>> _defaultCurrentMonthSpots = <List<double>>[
    <double>[1, 0],
    <double>[2, 0],
    <double>[3, 8],
    <double>[4, 8],
    <double>[5, 12],
    <double>[6, 12],
  ];

  static const List<List<double>> _defaultPreviousMonthSpots = <List<double>>[
    <double>[1, 0],
    <double>[2, 4],
    <double>[5, 11],
    <double>[14, 11],
    <double>[15, 28],
    <double>[22, 28],
    <double>[23, 35],
    <double>[25, 35],
    <double>[26, 45],
    <double>[31, 45],
  ];

  // Material Icons codePoints — kept here so the data layer doesn't depend on
  // Flutter's material library.
  static const int _iconShoppingBag = 0xf37b; // Icons.shopping_bag_outlined
  static const int _iconGroceryStore =
      0xe3ab; // Icons.local_grocery_store_outlined
  static const int _iconCar = 0xe1d0; // Icons.directions_car_outlined
  static const int _iconCart = 0xe8cc; // Icons.shopping_cart_outlined
  static const int _iconTaxi = 0xe531; // Icons.local_taxi_outlined
  static const int _iconVideo = 0xe63b; // Icons.ondemand_video_outlined
  static const int _iconPound = 0xead6; // Icons.currency_pound
  static const String _materialIcons = 'MaterialIcons';

  static const int _avatarBg = 0xFF1A1C20;
  static const int _avatarFg = 0xFF4ACB64;

  static const Map<String, SpendingCategoryDetail> _categories =
      <String, SpendingCategoryDetail>{
    'shopping': SpendingCategoryDetail(
      categoryId: 'shopping',
      title: 'Shopping',
      iconCodePoint: _iconShoppingBag,
      iconFontFamily: _materialIcons,
      monthLabel: 'March spend',
      totalAmount: '\u00A352.00',
      deltaAmount: '\u00A311.88',
      deltaReference: 'vs. 4 February',
      isDecrease: true,
      activeAlertCount: 1,
      transactionCountLabel: '1 Transaction',
      chartCurrentMonthSpots: _defaultCurrentMonthSpots,
      chartPreviousMonthSpots: _defaultPreviousMonthSpots,
      transactions: <SpendingCategoryTransaction>[
        SpendingCategoryTransaction(
          dateLabel: 'Mon 02 Mar',
          merchant: 'Uber Eats',
          amount: '\u00A352.00',
          time: '00:17',
          accountName: 'Current Account',
          accountBadge: 'S',
          avatarLabel: 'UE',
          avatarBackgroundValue: _avatarBg,
          avatarForegroundValue: _avatarFg,
        ),
      ],
    ),
    'groceries': SpendingCategoryDetail(
      categoryId: 'groceries',
      title: 'Groceries',
      iconCodePoint: _iconGroceryStore,
      iconFontFamily: _materialIcons,
      monthLabel: 'March spend',
      totalAmount: '\u00A3284.35',
      deltaAmount: '\u00A321.30',
      deltaReference: 'vs. 4 February',
      isDecrease: true,
      activeAlertCount: 1,
      transactionCountLabel: '1 Transaction',
      chartCurrentMonthSpots: _defaultCurrentMonthSpots,
      chartPreviousMonthSpots: _defaultPreviousMonthSpots,
      transactions: <SpendingCategoryTransaction>[
        SpendingCategoryTransaction(
          dateLabel: 'Mon 02 Mar',
          merchant: 'Tesco',
          amount: '\u00A3284.35',
          time: '14:22',
          accountName: 'Current Account',
          accountBadge: 'S',
          avatarLabel: 'T',
          avatarBackgroundValue: _avatarBg,
          avatarForegroundValue: _avatarFg,
        ),
      ],
    ),
    'transport': SpendingCategoryDetail(
      categoryId: 'transport',
      title: 'Transport',
      iconCodePoint: _iconCar,
      iconFontFamily: _materialIcons,
      monthLabel: 'March spend',
      totalAmount: '\u00A3126.40',
      deltaAmount: '\u00A318.00',
      deltaReference: 'vs. 4 February',
      isDecrease: false,
      activeAlertCount: 1,
      transactionCountLabel: '1 Transaction',
      chartCurrentMonthSpots: _defaultCurrentMonthSpots,
      chartPreviousMonthSpots: _defaultPreviousMonthSpots,
      transactions: <SpendingCategoryTransaction>[
        SpendingCategoryTransaction(
          dateLabel: 'Mon 02 Mar',
          merchant: 'Uber',
          amount: '\u00A3126.40',
          time: '09:05',
          accountName: 'Current Account',
          accountBadge: 'S',
          avatarLabel: 'U',
          avatarBackgroundValue: _avatarBg,
          avatarForegroundValue: _avatarFg,
        ),
      ],
    ),
    'amazon': SpendingCategoryDetail(
      categoryId: 'amazon',
      title: 'Amazon',
      iconCodePoint: _iconCart,
      iconFontFamily: _materialIcons,
      monthLabel: 'March spend',
      totalAmount: '\u00A3410.90',
      deltaAmount: '\u00A398.20',
      deltaReference: 'vs. 4 February',
      isDecrease: false,
      activeAlertCount: 1,
      transactionCountLabel: '1 Transaction',
      chartCurrentMonthSpots: _defaultCurrentMonthSpots,
      chartPreviousMonthSpots: _defaultPreviousMonthSpots,
      transactions: <SpendingCategoryTransaction>[
        SpendingCategoryTransaction(
          dateLabel: 'Mon 02 Mar',
          merchant: 'Amazon Marketplace',
          amount: '\u00A3410.90',
          time: '16:10',
          accountName: 'Current Account',
          accountBadge: 'S',
          avatarLabel: 'AM',
          avatarBackgroundValue: _avatarBg,
          avatarForegroundValue: _avatarFg,
        ),
      ],
    ),
    'tesco': SpendingCategoryDetail(
      categoryId: 'tesco',
      title: 'Tesco',
      iconCodePoint: _iconGroceryStore,
      iconFontFamily: _materialIcons,
      monthLabel: 'March spend',
      totalAmount: '\u00A3284.35',
      deltaAmount: '\u00A321.30',
      deltaReference: 'vs. 4 February',
      isDecrease: true,
      activeAlertCount: 1,
      transactionCountLabel: '1 Transaction',
      chartCurrentMonthSpots: _defaultCurrentMonthSpots,
      chartPreviousMonthSpots: _defaultPreviousMonthSpots,
      transactions: <SpendingCategoryTransaction>[
        SpendingCategoryTransaction(
          dateLabel: 'Mon 02 Mar',
          merchant: 'Tesco',
          amount: '\u00A3284.35',
          time: '14:22',
          accountName: 'Current Account',
          accountBadge: 'S',
          avatarLabel: 'TS',
          avatarBackgroundValue: _avatarBg,
          avatarForegroundValue: _avatarFg,
        ),
      ],
    ),
    'uber': SpendingCategoryDetail(
      categoryId: 'uber',
      title: 'Uber',
      iconCodePoint: _iconTaxi,
      iconFontFamily: _materialIcons,
      monthLabel: 'March spend',
      totalAmount: '\u00A3126.40',
      deltaAmount: '\u00A318.00',
      deltaReference: 'vs. 4 February',
      isDecrease: false,
      activeAlertCount: 1,
      transactionCountLabel: '1 Transaction',
      chartCurrentMonthSpots: _defaultCurrentMonthSpots,
      chartPreviousMonthSpots: _defaultPreviousMonthSpots,
      transactions: <SpendingCategoryTransaction>[
        SpendingCategoryTransaction(
          dateLabel: 'Mon 02 Mar',
          merchant: 'Uber',
          amount: '\u00A3126.40',
          time: '09:05',
          accountName: 'Current Account',
          accountBadge: 'S',
          avatarLabel: 'UB',
          avatarBackgroundValue: _avatarBg,
          avatarForegroundValue: _avatarFg,
        ),
      ],
    ),
    'netflix': SpendingCategoryDetail(
      categoryId: 'netflix',
      title: 'Netflix',
      iconCodePoint: _iconVideo,
      iconFontFamily: _materialIcons,
      monthLabel: 'March spend',
      totalAmount: '\u00A312.99',
      deltaAmount: '\u00A30.00',
      deltaReference: 'vs. 4 February',
      isDecrease: true,
      activeAlertCount: 1,
      transactionCountLabel: '1 Transaction',
      chartCurrentMonthSpots: _defaultCurrentMonthSpots,
      chartPreviousMonthSpots: _defaultPreviousMonthSpots,
      transactions: <SpendingCategoryTransaction>[
        SpendingCategoryTransaction(
          dateLabel: 'Mon 02 Mar',
          merchant: 'Netflix',
          amount: '\u00A312.99',
          time: '08:00',
          accountName: 'Current Account',
          accountBadge: 'S',
          avatarLabel: 'N',
          avatarBackgroundValue: _avatarBg,
          avatarForegroundValue: _avatarFg,
        ),
      ],
    ),
    'finances': SpendingCategoryDetail(
      categoryId: 'finances',
      title: 'Finances',
      iconCodePoint: _iconPound,
      iconFontFamily: _materialIcons,
      monthLabel: 'March spend',
      totalAmount: '\u00A3148.60',
      deltaAmount: '\u00A39.20',
      deltaReference: 'vs. 4 February',
      isDecrease: true,
      activeAlertCount: 1,
      transactionCountLabel: '1 Transaction',
      chartCurrentMonthSpots: _defaultCurrentMonthSpots,
      chartPreviousMonthSpots: _defaultPreviousMonthSpots,
      transactions: <SpendingCategoryTransaction>[
        SpendingCategoryTransaction(
          dateLabel: 'Mon 02 Mar',
          merchant: 'Transfer fee',
          amount: '\u00A3148.60',
          time: '11:45',
          accountName: 'Current Account',
          accountBadge: 'S',
          avatarLabel: 'TF',
          avatarBackgroundValue: _avatarBg,
          avatarForegroundValue: _avatarFg,
        ),
      ],
    ),
  };

  @override
  Future<SpendingCategoryDetail?> getCategoryDetail(String categoryId) async {
    await MockBehavior.delay();
    MockBehavior.throwIfEnabled('spendingCategory.getCategoryDetail');

    if (demoDataMode == DemoDataMode.fresh) {
      // Return a skeletal detail with no transactions so the screen can
      // display the fresh-demo empty state.
      final SpendingCategoryDetail? populated = _categories[categoryId];
      if (populated == null) {
        return null;
      }

      return SpendingCategoryDetail(
        categoryId: populated.categoryId,
        title: populated.title,
        iconCodePoint: populated.iconCodePoint,
        iconFontFamily: populated.iconFontFamily,
        monthLabel: populated.monthLabel,
        totalAmount: '\u00A30.00',
        deltaAmount: '\u00A30.00',
        deltaReference: '',
        isDecrease: true,
        activeAlertCount: 0,
        transactionCountLabel: '0 Transactions',
        chartCurrentMonthSpots: const <List<double>>[],
        chartPreviousMonthSpots: const <List<double>>[],
        transactions: const <SpendingCategoryTransaction>[],
      );
    }

    return _categories[categoryId] ?? _categories['finances'];
  }
}
