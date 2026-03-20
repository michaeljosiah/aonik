import '../../app/demo/demo_data_mode.dart';
import '../../data/repositories/account_links_repository.dart';
import '../../data/repositories/spending_category_repository.dart';
import '../mock_behavior.dart';

class MockSpendingCategoryRepository implements SpendingCategoryRepository {
  MockSpendingCategoryRepository({
    this.demoDataMode = DemoDataMode.populated,
    Set<String> Function()? activeConnectionIdsGetter,
    List<AccountLinkItem> Function()? runtimeAccountsGetter,
  }) : _activeConnectionIdsGetter = activeConnectionIdsGetter,
       _runtimeAccountsGetter = runtimeAccountsGetter,
       _categories = demoDataMode == DemoDataMode.fresh
            ? <String, _MutableCategoryData>{}
            : Map<String, _MutableCategoryData>.fromEntries(
                _seedCategories.entries.map(
                  (MapEntry<String, _MutableCategoryData> entry) =>
                      MapEntry<String, _MutableCategoryData>(
                    entry.key,
                    _MutableCategoryData(
                      detail: entry.value.detail,
                      transactions: List<SpendingCategoryTransaction>.of(
                          entry.value.transactions),
                    ),
                  ),
                ),
              );

  final DemoDataMode demoDataMode;

  /// When non-null, called at query time to resolve the current set of active
  /// connection IDs. Only transactions whose [connectionId] appears in the
  /// returned set (or whose connectionId is null) are returned. Categories
  /// with zero remaining transactions are returned with zeroed-out totals.
  final Set<String> Function()? _activeConnectionIdsGetter;

  /// When non-null, called at query time to retrieve runtime-created accounts.
  /// Reserved for future use — runtime accounts currently have no categorised
  /// transactions, but this callback enables the repository to participate in
  /// cross-repository coordination (e.g. provider invalidation).
  // ignore: unused_field
  final List<AccountLinkItem> Function()? _runtimeAccountsGetter;

  final Map<String, _MutableCategoryData> _categories;

  // ─────────────────────────────────────────────────────────
  //  Filtering helper
  // ─────────────────────────────────────────────────────────

  bool _isConnectionActive(String? connectionId) {
    if (_activeConnectionIdsGetter == null) return true;
    if (connectionId == null) return true;
    return _activeConnectionIdsGetter().contains(connectionId);
  }

  // ─────────────────────────────────────────────────────────
  //  Constants
  // ─────────────────────────────────────────────────────────

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

  // ─────────────────────────────────────────────────────────
  //  Connection IDs (must match mock_account_links_repository)
  // ─────────────────────────────────────────────────────────

  static const String _connStarling = 'mock-connection-starling';
  static const String _connAmex = 'mock-connection-amex';
  static const String _connGtbank = 'mock-connection-gtbank';

  // ─────────────────────────────────────────────────────────
  //  Seed data
  // ─────────────────────────────────────────────────────────

  static final Map<String, _MutableCategoryData> _seedCategories =
      <String, _MutableCategoryData>{
    'shopping': _MutableCategoryData(
      detail: const SpendingCategoryDetail(
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
        transactions: <SpendingCategoryTransaction>[],
      ),
      transactions: <SpendingCategoryTransaction>[
        const SpendingCategoryTransaction(
          dateLabel: 'Mon 02 Mar',
          merchant: 'Uber Eats',
          amount: '\u00A352.00',
          time: '00:17',
          accountName: 'Current Account',
          accountBadge: 'S',
          avatarLabel: 'UE',
          avatarBackgroundValue: _avatarBg,
          avatarForegroundValue: _avatarFg,
          connectionId: _connStarling,
        ),
      ],
    ),
    'groceries': _MutableCategoryData(
      detail: const SpendingCategoryDetail(
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
        transactions: <SpendingCategoryTransaction>[],
      ),
      transactions: <SpendingCategoryTransaction>[
        const SpendingCategoryTransaction(
          dateLabel: 'Mon 02 Mar',
          merchant: 'Tesco',
          amount: '\u00A3284.35',
          time: '14:22',
          accountName: 'Current Account',
          accountBadge: 'S',
          avatarLabel: 'T',
          avatarBackgroundValue: _avatarBg,
          avatarForegroundValue: _avatarFg,
          connectionId: _connStarling,
        ),
      ],
    ),
    'transport': _MutableCategoryData(
      detail: const SpendingCategoryDetail(
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
        transactions: <SpendingCategoryTransaction>[],
      ),
      transactions: <SpendingCategoryTransaction>[
        const SpendingCategoryTransaction(
          dateLabel: 'Mon 02 Mar',
          merchant: 'Uber',
          amount: '\u00A3126.40',
          time: '09:05',
          accountName: 'Current Account',
          accountBadge: 'S',
          avatarLabel: 'U',
          avatarBackgroundValue: _avatarBg,
          avatarForegroundValue: _avatarFg,
          connectionId: _connStarling,
        ),
      ],
    ),
    'amazon': _MutableCategoryData(
      detail: const SpendingCategoryDetail(
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
        transactions: <SpendingCategoryTransaction>[],
      ),
      transactions: <SpendingCategoryTransaction>[
        const SpendingCategoryTransaction(
          dateLabel: 'Mon 02 Mar',
          merchant: 'Amazon Marketplace',
          amount: '\u00A3410.90',
          time: '16:10',
          accountName: 'Current Account',
          accountBadge: 'S',
          avatarLabel: 'AM',
          avatarBackgroundValue: _avatarBg,
          avatarForegroundValue: _avatarFg,
          connectionId: _connStarling,
        ),
      ],
    ),
    'tesco': _MutableCategoryData(
      detail: const SpendingCategoryDetail(
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
        transactions: <SpendingCategoryTransaction>[],
      ),
      transactions: <SpendingCategoryTransaction>[
        const SpendingCategoryTransaction(
          dateLabel: 'Mon 02 Mar',
          merchant: 'Tesco',
          amount: '\u00A3284.35',
          time: '14:22',
          accountName: 'Current Account',
          accountBadge: 'S',
          avatarLabel: 'TS',
          avatarBackgroundValue: _avatarBg,
          avatarForegroundValue: _avatarFg,
          connectionId: _connStarling,
        ),
      ],
    ),
    'uber': _MutableCategoryData(
      detail: const SpendingCategoryDetail(
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
        transactions: <SpendingCategoryTransaction>[],
      ),
      transactions: <SpendingCategoryTransaction>[
        const SpendingCategoryTransaction(
          dateLabel: 'Mon 02 Mar',
          merchant: 'Uber',
          amount: '\u00A3126.40',
          time: '09:05',
          accountName: 'Current Account',
          accountBadge: 'S',
          avatarLabel: 'UB',
          avatarBackgroundValue: _avatarBg,
          avatarForegroundValue: _avatarFg,
          connectionId: _connStarling,
        ),
      ],
    ),
    'netflix': _MutableCategoryData(
      detail: const SpendingCategoryDetail(
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
        transactions: <SpendingCategoryTransaction>[],
      ),
      transactions: <SpendingCategoryTransaction>[
        const SpendingCategoryTransaction(
          dateLabel: 'Mon 02 Mar',
          merchant: 'Netflix',
          amount: '\u00A312.99',
          time: '08:00',
          accountName: 'Credit Card',
          accountBadge: 'AX',
          avatarLabel: 'N',
          avatarBackgroundValue: _avatarBg,
          avatarForegroundValue: _avatarFg,
          connectionId: _connAmex,
        ),
      ],
    ),
    'finances': _MutableCategoryData(
      detail: const SpendingCategoryDetail(
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
        transactions: <SpendingCategoryTransaction>[],
      ),
      transactions: <SpendingCategoryTransaction>[
        const SpendingCategoryTransaction(
          dateLabel: 'Mon 02 Mar',
          merchant: 'Transfer fee',
          amount: '\u00A3148.60',
          time: '11:45',
          accountName: 'Naira Current',
          accountBadge: 'GT',
          avatarLabel: 'TF',
          avatarBackgroundValue: _avatarBg,
          avatarForegroundValue: _avatarFg,
          connectionId: _connGtbank,
        ),
      ],
    ),
  };

  // ─────────────────────────────────────────────────────────
  //  Repository implementation
  // ─────────────────────────────────────────────────────────

  @override
  Future<SpendingCategoryDetail?> getCategoryDetail(String categoryId) async {
    await MockBehavior.delay();
    MockBehavior.throwIfEnabled('spendingCategory.getCategoryDetail');

    if (demoDataMode == DemoDataMode.fresh) {
      final _MutableCategoryData? data = _categories[categoryId];
      // In fresh mode, fall back to seed data for the skeleton detail.
      final SpendingCategoryDetail? populated =
          data?.detail ?? _seedCategories[categoryId]?.detail;
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

    final _MutableCategoryData? data = _categories[categoryId];
    if (data == null) {
      // Fall back to 'finances' as the original code did.
      final _MutableCategoryData? fallback = _categories['finances'];
      if (fallback == null) return null;
      return _buildFilteredDetail(fallback);
    }

    return _buildFilteredDetail(data);
  }

  /// Builds a [SpendingCategoryDetail] from mutable data, filtering
  /// transactions by [activeConnectionIds].
  SpendingCategoryDetail _buildFilteredDetail(_MutableCategoryData data) {
    final List<SpendingCategoryTransaction> filteredTxns = data.transactions
        .where((SpendingCategoryTransaction t) =>
            _isConnectionActive(t.connectionId))
        .toList();

    final int count = filteredTxns.length;
    final String countLabel =
        count == 1 ? '1 Transaction' : '$count Transactions';

    return SpendingCategoryDetail(
      categoryId: data.detail.categoryId,
      title: data.detail.title,
      iconCodePoint: data.detail.iconCodePoint,
      iconFontFamily: data.detail.iconFontFamily,
      monthLabel: data.detail.monthLabel,
      totalAmount: filteredTxns.isEmpty ? '\u00A30.00' : data.detail.totalAmount,
      deltaAmount: filteredTxns.isEmpty ? '\u00A30.00' : data.detail.deltaAmount,
      deltaReference:
          filteredTxns.isEmpty ? '' : data.detail.deltaReference,
      isDecrease: data.detail.isDecrease,
      activeAlertCount:
          filteredTxns.isEmpty ? 0 : data.detail.activeAlertCount,
      transactionCountLabel: countLabel,
      chartCurrentMonthSpots: filteredTxns.isEmpty
          ? const <List<double>>[]
          : data.detail.chartCurrentMonthSpots,
      chartPreviousMonthSpots: filteredTxns.isEmpty
          ? const <List<double>>[]
          : data.detail.chartPreviousMonthSpots,
      transactions: filteredTxns,
    );
  }
}

/// Internal holder to separate the immutable detail metadata from the mutable
/// list of transactions.
class _MutableCategoryData {
  _MutableCategoryData({
    required this.detail,
    required this.transactions,
  });

  /// Contains the category metadata (title, icon, chart spots, etc.).
  /// The [SpendingCategoryDetail.transactions] list on this object is ignored;
  /// we use the mutable [transactions] list below instead.
  final SpendingCategoryDetail detail;

  /// Mutable, filterable list of transactions for this category.
  final List<SpendingCategoryTransaction> transactions;
}
