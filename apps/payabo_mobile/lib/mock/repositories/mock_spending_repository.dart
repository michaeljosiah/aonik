import '../../app/demo/demo_data_mode.dart';
import '../../data/repositories/account_links_repository.dart';
import '../../data/repositories/spending_repository.dart';
import '../mock_behavior.dart';

class MockSpendingRepository implements SpendingRepository {
  MockSpendingRepository({
    this.demoDataMode = DemoDataMode.populated,
    Set<String> Function()? activeConnectionIdsGetter,
    List<AccountLinkItem> Function()? runtimeAccountsGetter,
  })  : _activeConnectionIdsGetter = activeConnectionIdsGetter,
        _runtimeAccountsGetter = runtimeAccountsGetter,
        _accounts = demoDataMode == DemoDataMode.fresh
            ? <SpendingAccountCard>[]
            : List<SpendingAccountCard>.of(_seedAccounts),
        _transactions = demoDataMode == DemoDataMode.fresh
            ? <String, List<SpendingTransaction>>{}
            : Map<String, List<SpendingTransaction>>.fromEntries(
                _seedTransactions.entries.map(
                  (MapEntry<String, List<SpendingTransaction>> entry) =>
                      MapEntry<String, List<SpendingTransaction>>(
                    entry.key,
                    List<SpendingTransaction>.of(entry.value),
                  ),
                ),
              ),
        _overviewSnapshots = demoDataMode == DemoDataMode.fresh
            ? <SpendingAccountSnapshot>[]
            : List<SpendingAccountSnapshot>.of(_seedOverviewSnapshots);

  final DemoDataMode demoDataMode;

  /// When non-null, called at query time to resolve the current set of active
  /// connection IDs. Only accounts/transactions whose [connectionId] appears
  /// in the returned set (or whose connectionId is null) are returned. This
  /// enables cross-repository coordination: when an account link is
  /// disconnected, the spending repository automatically filters out its data.
  final Set<String> Function()? _activeConnectionIdsGetter;

  /// When non-null, called at query time to retrieve all runtime-created
  /// accounts (both linked via open-banking and added manually) from the
  /// account links repository. These accounts have no corresponding seed data
  /// in [_seedAccounts] and are synthesised into [SpendingAccountCard] and
  /// [SpendingAccountSnapshot] entries so the spending screen reflects newly
  /// linked or manually added accounts.
  final List<AccountLinkItem> Function()? _runtimeAccountsGetter;

  final List<SpendingAccountCard> _accounts;
  final Map<String, List<SpendingTransaction>> _transactions;
  final List<SpendingAccountSnapshot> _overviewSnapshots;

  /// In-memory storage for manually-added transactions (keyed by accountId).
  /// These are merged into [getTransactions] results for manual accounts.
  final Map<String, List<SpendingTransaction>> _manualTransactions =
      <String, List<SpendingTransaction>>{};

  /// Auto-incrementing counter for generating unique manual transaction IDs.
  int _manualTxCounter = 0;

  // ─────────────────────────────────────────────────────────
  //  Filtering helper
  // ─────────────────────────────────────────────────────────

  bool _isConnectionActive(String? connectionId) {
    if (_activeConnectionIdsGetter == null) return true;
    if (connectionId == null) return true;
    return _activeConnectionIdsGetter().contains(connectionId);
  }

  static String _currencySymbolFromCode(String code) {
    switch (code.toUpperCase()) {
      case 'GBP':
        return '\u00A3';
      case 'USD':
        return '\$';
      case 'EUR':
        return '\u20AC';
      case 'NGN':
        return '\u20A6';
      case 'KES':
        return 'KSh';
      case 'GHS':
        return 'GH\u20B5';
      case 'ZAR':
        return 'R';
      case 'CAD':
        return 'CA\$';
      case 'INR':
        return '\u20B9';
      default:
        return code;
    }
  }

  // ─────────────────────────────────────────────────────────
  //  Icon code points (MaterialIcons font family)
  // ─────────────────────────────────────────────────────────

  static const String _mi = 'MaterialIcons';

  // Icons.star_border_rounded
  static const int _iconStarBorderRounded = 0xf01cf;
  // Icons.credit_card_outlined
  static const int _iconCreditCardOutlined = 0xef8f;
  // Icons.home_outlined
  static const int _iconHomeOutlined = 0xf107;
  // Icons.account_balance_wallet_outlined
  static const int _iconWalletOutlined = 0xee33;
  // Icons.savings_outlined
  static const int _iconSavingsOutlined = 0xf336;
  // Icons.stacked_line_chart
  static const int _iconStackedLineChart = 0xe5f7;
  // Icons.diamond_outlined
  static const int _iconDiamondOutlined = 0xf05e7;
  // Icons.account_balance_outlined
  static const int _iconAccountBalanceOutlined = 0xee2f;
  // Icons.currency_exchange
  static const int _iconCurrencyExchange = 0xf05b4;
  // Icons.edit_outlined
  static const int _iconEditOutlined = 0xef4b;

  // ─────────────────────────────────────────────────────────
  //  Connection IDs (must match mock_account_links_repository)
  // ─────────────────────────────────────────────────────────

  static const String _connStarling = 'mock-connection-starling';
  static const String _connAmex = 'mock-connection-amex';
  static const String _connGtbank = 'mock-connection-gtbank';
  static const String _connKuda = 'mock-connection-kuda';
  static const String _connAccess = 'mock-connection-access';

  // ─────────────────────────────────────────────────────────
  //  Seed data — accounts
  // ─────────────────────────────────────────────────────────

  static const List<SpendingAccountCard> _seedAccounts = <SpendingAccountCard>[
    // ── UK accounts ──
    SpendingAccountCard(
      id: 'uk-current',
      accountName: 'UK Current',
      providerName: 'Starling',
      providerIconCodePoint: _iconStarBorderRounded,
      providerIconFontFamily: _mi,
      balanceLabel: '\u00A33,842.16',
      balanceMajor: '3,842',
      balanceMinor: '.16',
      currencySymbol: '\u00A3',
      connectionId: _connStarling,
    ),
    SpendingAccountCard(
      id: 'uk-savings',
      accountName: 'UK Savings',
      providerName: 'Starling',
      providerIconCodePoint: _iconSavingsOutlined,
      providerIconFontFamily: _mi,
      balanceLabel: '\u00A36,240.00',
      balanceMajor: '6,240',
      balanceMinor: '.00',
      currencySymbol: '\u00A3',
      connectionId: _connStarling,
    ),
    SpendingAccountCard(
      id: 'uk-credit',
      accountName: 'Credit Card',
      providerName: 'Amex',
      providerIconCodePoint: _iconCreditCardOutlined,
      providerIconFontFamily: _mi,
      balanceLabel: '-\u00A3842.30',
      balanceMajor: '-842',
      balanceMinor: '.30',
      currencySymbol: '\u00A3',
      connectionId: _connAmex,
    ),
    // ── Nigeria accounts ──
    SpendingAccountCard(
      id: 'ng-current',
      accountName: 'Naira Current',
      providerName: 'GTBank',
      providerIconCodePoint: _iconAccountBalanceOutlined,
      providerIconFontFamily: _mi,
      balanceLabel: '\u20A6485,200.00',
      balanceMajor: '485,200',
      balanceMinor: '.00',
      currencySymbol: '\u20A6',
      connectionId: _connGtbank,
    ),
    SpendingAccountCard(
      id: 'ng-savings',
      accountName: 'Naira Savings',
      providerName: 'Kuda',
      providerIconCodePoint: _iconSavingsOutlined,
      providerIconFontFamily: _mi,
      balanceLabel: '\u20A61,240,000.00',
      balanceMajor: '1,240,000',
      balanceMinor: '.00',
      currencySymbol: '\u20A6',
      connectionId: _connKuda,
    ),
    SpendingAccountCard(
      id: 'ng-domiciliary',
      accountName: 'Dollar Dom.',
      providerName: 'Access Bank',
      providerIconCodePoint: _iconCurrencyExchange,
      providerIconFontFamily: _mi,
      balanceLabel: '\$2,150.00',
      balanceMajor: '2,150',
      balanceMinor: '.00',
      currencySymbol: '\$',
      connectionId: _connAccess,
    ),
  ];

  // ─────────────────────────────────────────────────────────
  //  getAccounts
  // ─────────────────────────────────────────────────────────

  @override
  Future<List<SpendingAccountCard>> getAccounts() async {
    await MockBehavior.delay();
    MockBehavior.throwIfEnabled('spending.getAccounts');

    final List<SpendingAccountCard> results;

    if (demoDataMode == DemoDataMode.fresh) {
      results = <SpendingAccountCard>[];
    } else {
      results = _accounts
          .where(
              (SpendingAccountCard a) => _isConnectionActive(a.connectionId))
          .toList();
    }

    // Include runtime-created accounts (linked via open-banking or added
    // manually) from the account links repository that have no corresponding
    // seed entry in [_accounts]. This ensures the spending screen shows the
    // populated state after an account is linked or manually created — even
    // in `DemoDataMode.fresh` where the seed list is empty.
    final List<AccountLinkItem> runtimeAccounts =
        _runtimeAccountsGetter?.call() ?? const <AccountLinkItem>[];

    final Set<String> existingIds =
        results.map((SpendingAccountCard a) => a.id).toSet();

    for (final AccountLinkItem item in runtimeAccounts) {
      if (existingIds.contains(item.id)) continue;

      final String symbol = _currencySymbolFromCode(item.currencyCode);
      final String balanceMajor;
      final String balanceMinor;

      if (item.balanceLabel != null && item.balanceLabel!.contains('.')) {
        // Strip leading currency symbol(s) and whitespace for the major part.
        final String raw = item.balanceLabel!
            .replaceAll(RegExp(r'^[^0-9\-]*'), '');
        final int rawDot = raw.lastIndexOf('.');
        balanceMajor = rawDot >= 0 ? raw.substring(0, rawDot) : raw;
        balanceMinor = rawDot >= 0 ? raw.substring(rawDot) : '.00';
      } else {
        balanceMajor = '0';
        balanceMinor = '.00';
      }

      // Use a different icon for linked accounts vs manual accounts.
      final bool isManual = item.source == AccountLinkSource.manual;
      final int iconCodePoint =
          isManual ? _iconEditOutlined : _iconAccountBalanceOutlined;

      results.add(
        SpendingAccountCard(
          id: item.id,
          accountName: item.name,
          providerName: item.providerLabel ?? (isManual ? 'Manual' : item.institutionName),
          providerIconCodePoint: iconCodePoint,
          providerIconFontFamily: _mi,
          balanceLabel: item.balanceLabel ?? '${symbol}0.00',
          balanceMajor: balanceMajor,
          balanceMinor: balanceMinor,
          currencySymbol: symbol,
          currencyCode: item.currencyCode,
          connectionId: item.connectionId,
          isManual: isManual,
        ),
      );
    }

    return results;
  }

  // ─────────────────────────────────────────────────────────
  //  Seed data — transactions
  // ─────────────────────────────────────────────────────────

  static final Map<String, List<SpendingTransaction>> _seedTransactions =
      <String, List<SpendingTransaction>>{
    // ── UK Current (15 transactions) ──
    'uk-current': <SpendingTransaction>[
      SpendingTransaction(
        id: 'uk-t01',
        merchant: 'Open Rent',
        category: 'housing',
        subCategory: 'rent',
        amountLabel: '+\u00A31,450.00',
        amountMajor: '1,450',
        amountMinor: '.00',
        currencySymbol: '\u00A3',
        isCredit: true,
        date: DateTime(2026, 3, 17),
        iconCodePoint: _iconHomeOutlined,
        iconFontFamily: _mi,
        connectionId: _connStarling,
      ),
      SpendingTransaction(
        id: 'uk-t02',
        merchant: 'Tesco',
        category: 'groceries',
        subCategory: 'supermarket',
        amountLabel: '-\u00A354.12',
        amountMajor: '54',
        amountMinor: '.12',
        currencySymbol: '\u00A3',
        isCredit: false,
        date: DateTime(2026, 3, 16),
        iconText: 'T',
        connectionId: _connStarling,
      ),
      SpendingTransaction(
        id: 'uk-t03',
        merchant: 'Uber',
        category: 'transport',
        subCategory: 'ride_hailing',
        amountLabel: '-\u00A314.20',
        amountMajor: '14',
        amountMinor: '.20',
        currencySymbol: '\u00A3',
        isCredit: false,
        date: DateTime(2026, 3, 16),
        iconText: 'U',
        connectionId: _connStarling,
      ),
      SpendingTransaction(
        id: 'uk-t04',
        merchant: 'Amazon',
        category: 'shopping',
        subCategory: 'online',
        amountLabel: '-\u00A327.99',
        amountMajor: '27',
        amountMinor: '.99',
        currencySymbol: '\u00A3',
        isCredit: false,
        date: DateTime(2026, 3, 15),
        iconText: 'a',
        connectionId: _connStarling,
      ),
      SpendingTransaction(
        id: 'uk-t05',
        merchant: "Nando's",
        category: 'eating_out',
        subCategory: 'restaurant',
        amountLabel: '-\u00A328.45',
        amountMajor: '28',
        amountMinor: '.45',
        currencySymbol: '\u00A3',
        isCredit: false,
        date: DateTime(2026, 3, 14),
        iconText: 'N',
        connectionId: _connStarling,
      ),
      SpendingTransaction(
        id: 'uk-t06',
        merchant: 'TfL',
        category: 'transport',
        subCategory: 'public_transit',
        amountLabel: '-\u00A37.40',
        amountMajor: '7',
        amountMinor: '.40',
        currencySymbol: '\u00A3',
        isCredit: false,
        date: DateTime(2026, 3, 13),
        iconText: 'TL',
        connectionId: _connStarling,
      ),
      SpendingTransaction(
        id: 'uk-t07',
        merchant: 'Sainsbury\'s',
        category: 'groceries',
        subCategory: 'supermarket',
        amountLabel: '-\u00A362.30',
        amountMajor: '62',
        amountMinor: '.30',
        currencySymbol: '\u00A3',
        isCredit: false,
        date: DateTime(2026, 3, 12),
        iconText: 'S',
        connectionId: _connStarling,
      ),
      SpendingTransaction(
        id: 'uk-t08',
        merchant: 'Shell',
        category: 'transport',
        subCategory: 'fuel',
        amountLabel: '-\u00A358.40',
        amountMajor: '58',
        amountMinor: '.40',
        currencySymbol: '\u00A3',
        isCredit: false,
        date: DateTime(2026, 3, 11),
        iconText: 'SH',
        connectionId: _connStarling,
      ),
      SpendingTransaction(
        id: 'uk-t09',
        merchant: 'Gym Group',
        category: 'health',
        subCategory: 'gym',
        amountLabel: '-\u00A324.99',
        amountMajor: '24',
        amountMinor: '.99',
        currencySymbol: '\u00A3',
        isCredit: false,
        date: DateTime(2026, 3, 10),
        iconText: 'GG',
        connectionId: _connStarling,
      ),
      SpendingTransaction(
        id: 'uk-t10',
        merchant: 'Boots',
        category: 'health',
        subCategory: 'pharmacy',
        amountLabel: '-\u00A312.50',
        amountMajor: '12',
        amountMinor: '.50',
        currencySymbol: '\u00A3',
        isCredit: false,
        date: DateTime(2026, 3, 9),
        iconText: 'B',
        connectionId: _connStarling,
      ),
      SpendingTransaction(
        id: 'uk-t11',
        merchant: 'Deliveroo',
        category: 'eating_out',
        subCategory: 'delivery',
        amountLabel: '-\u00A319.80',
        amountMajor: '19',
        amountMinor: '.80',
        currencySymbol: '\u00A3',
        isCredit: false,
        date: DateTime(2026, 3, 8),
        iconText: 'D',
        connectionId: _connStarling,
      ),
      SpendingTransaction(
        id: 'uk-t12',
        merchant: 'British Gas',
        category: 'bills',
        subCategory: 'gas',
        amountLabel: '-\u00A386.00',
        amountMajor: '86',
        amountMinor: '.00',
        currencySymbol: '\u00A3',
        isCredit: false,
        date: DateTime(2026, 3, 7),
        iconText: 'BG',
        connectionId: _connStarling,
      ),
      SpendingTransaction(
        id: 'uk-t13',
        merchant: 'Pret A Manger',
        category: 'eating_out',
        subCategory: 'cafe',
        amountLabel: '-\u00A35.60',
        amountMajor: '5',
        amountMinor: '.60',
        currencySymbol: '\u00A3',
        isCredit: false,
        date: DateTime(2026, 3, 6),
        iconText: 'P',
        connectionId: _connStarling,
      ),
      SpendingTransaction(
        id: 'uk-t14',
        merchant: 'Vodafone',
        category: 'bills',
        subCategory: 'phone',
        amountLabel: '-\u00A325.00',
        amountMajor: '25',
        amountMinor: '.00',
        currencySymbol: '\u00A3',
        isCredit: false,
        date: DateTime(2026, 3, 5),
        iconText: 'V',
        connectionId: _connStarling,
      ),
      SpendingTransaction(
        id: 'uk-t15',
        merchant: 'John Lewis',
        category: 'shopping',
        subCategory: 'department_store',
        amountLabel: '-\u00A389.00',
        amountMajor: '89',
        amountMinor: '.00',
        currencySymbol: '\u00A3',
        isCredit: false,
        date: DateTime(2026, 3, 4),
        iconText: 'JL',
        connectionId: _connStarling,
      ),
    ],
    // ── UK Savings (3 transactions) ──
    'uk-savings': <SpendingTransaction>[
      SpendingTransaction(
        id: 'uks-t01',
        merchant: 'Auto-save',
        category: 'savings',
        subCategory: 'goal_savings',
        amountLabel: '+\u00A3200.00',
        amountMajor: '200',
        amountMinor: '.00',
        currencySymbol: '\u00A3',
        isCredit: true,
        date: DateTime(2026, 3, 15),
        iconCodePoint: _iconSavingsOutlined,
        iconFontFamily: _mi,
        connectionId: _connStarling,
      ),
      SpendingTransaction(
        id: 'uks-t02',
        merchant: 'Starling Interest',
        category: 'income',
        subCategory: 'interest',
        amountLabel: '+\u00A34.80',
        amountMajor: '4',
        amountMinor: '.80',
        currencySymbol: '\u00A3',
        isCredit: true,
        date: DateTime(2026, 3, 1),
        iconCodePoint: _iconSavingsOutlined,
        iconFontFamily: _mi,
        connectionId: _connStarling,
      ),
      SpendingTransaction(
        id: 'uks-t03',
        merchant: 'Auto-save',
        category: 'savings',
        subCategory: 'goal_savings',
        amountLabel: '+\u00A3200.00',
        amountMajor: '200',
        amountMinor: '.00',
        currencySymbol: '\u00A3',
        isCredit: true,
        date: DateTime(2026, 2, 15),
        iconCodePoint: _iconSavingsOutlined,
        iconFontFamily: _mi,
        connectionId: _connStarling,
      ),
    ],
    // ── UK Credit Card (6 transactions) ──
    'uk-credit': <SpendingTransaction>[
      SpendingTransaction(
        id: 'ukc-t01',
        merchant: 'Netflix',
        category: 'subscriptions',
        subCategory: 'streaming',
        amountLabel: '-\u00A315.99',
        amountMajor: '15',
        amountMinor: '.99',
        currencySymbol: '\u00A3',
        isCredit: false,
        date: DateTime(2026, 3, 15),
        iconText: 'N',
        connectionId: _connAmex,
      ),
      SpendingTransaction(
        id: 'ukc-t02',
        merchant: 'Spotify',
        category: 'subscriptions',
        subCategory: 'streaming',
        amountLabel: '-\u00A310.99',
        amountMajor: '10',
        amountMinor: '.99',
        currencySymbol: '\u00A3',
        isCredit: false,
        date: DateTime(2026, 3, 14),
        iconText: 'S',
        connectionId: _connAmex,
      ),
      SpendingTransaction(
        id: 'ukc-t03',
        merchant: 'Apple iCloud',
        category: 'subscriptions',
        subCategory: 'cloud_storage',
        amountLabel: '-\u00A30.99',
        amountMajor: '0',
        amountMinor: '.99',
        currencySymbol: '\u00A3',
        isCredit: false,
        date: DateTime(2026, 3, 12),
        iconText: 'A',
        connectionId: _connAmex,
      ),
      SpendingTransaction(
        id: 'ukc-t04',
        merchant: 'Amazon Prime',
        category: 'subscriptions',
        subCategory: 'streaming',
        amountLabel: '-\u00A38.99',
        amountMajor: '8',
        amountMinor: '.99',
        currencySymbol: '\u00A3',
        isCredit: false,
        date: DateTime(2026, 3, 10),
        iconText: 'AP',
        connectionId: _connAmex,
      ),
      SpendingTransaction(
        id: 'ukc-t05',
        merchant: 'ASOS',
        category: 'shopping',
        subCategory: 'clothing',
        amountLabel: '-\u00A345.60',
        amountMajor: '45',
        amountMinor: '.60',
        currencySymbol: '\u00A3',
        isCredit: false,
        date: DateTime(2026, 3, 8),
        iconText: 'AS',
        connectionId: _connAmex,
      ),
      SpendingTransaction(
        id: 'ukc-t06',
        merchant: 'EasyJet',
        category: 'travel',
        subCategory: 'flights',
        amountLabel: '-\u00A3189.00',
        amountMajor: '189',
        amountMinor: '.00',
        currencySymbol: '\u00A3',
        isCredit: false,
        date: DateTime(2026, 3, 5),
        iconText: 'EJ',
        connectionId: _connAmex,
      ),
    ],
    // ── Nigeria Current (12 transactions) ──
    'ng-current': <SpendingTransaction>[
      SpendingTransaction(
        id: 'ng-t01',
        merchant: 'Shoprite Lekki',
        category: 'groceries',
        subCategory: 'supermarket',
        amountLabel: '-\u20A618,500.00',
        amountMajor: '18,500',
        amountMinor: '.00',
        currencySymbol: '\u20A6',
        isCredit: false,
        date: DateTime(2026, 3, 17),
        iconText: 'SR',
        connectionId: _connGtbank,
      ),
      SpendingTransaction(
        id: 'ng-t02',
        merchant: 'Eko Electricity',
        category: 'bills',
        subCategory: 'electricity',
        amountLabel: '-\u20A612,000.00',
        amountMajor: '12,000',
        amountMinor: '.00',
        currencySymbol: '\u20A6',
        isCredit: false,
        date: DateTime(2026, 3, 16),
        iconText: 'EE',
        connectionId: _connGtbank,
      ),
      SpendingTransaction(
        id: 'ng-t03',
        merchant: 'Bolt',
        category: 'transport',
        subCategory: 'ride_hailing',
        amountLabel: '-\u20A64,200.00',
        amountMajor: '4,200',
        amountMinor: '.00',
        currencySymbol: '\u20A6',
        isCredit: false,
        date: DateTime(2026, 3, 15),
        iconText: 'BT',
        connectionId: _connGtbank,
      ),
      SpendingTransaction(
        id: 'ng-t04',
        merchant: 'MTN Data',
        category: 'bills',
        subCategory: 'internet',
        amountLabel: '-\u20A63,500.00',
        amountMajor: '3,500',
        amountMinor: '.00',
        currencySymbol: '\u20A6',
        isCredit: false,
        date: DateTime(2026, 3, 14),
        iconText: 'MT',
        connectionId: _connGtbank,
      ),
      SpendingTransaction(
        id: 'ng-t05',
        merchant: 'Chicken Republic',
        category: 'eating_out',
        subCategory: 'fast_food',
        amountLabel: '-\u20A65,800.00',
        amountMajor: '5,800',
        amountMinor: '.00',
        currencySymbol: '\u20A6',
        isCredit: false,
        date: DateTime(2026, 3, 13),
        iconText: 'CR',
        connectionId: _connGtbank,
      ),
      SpendingTransaction(
        id: 'ng-t06',
        merchant: 'Salary Credit',
        category: 'income',
        subCategory: 'salary',
        amountLabel: '+\u20A6750,000.00',
        amountMajor: '750,000',
        amountMinor: '.00',
        currencySymbol: '\u20A6',
        isCredit: true,
        date: DateTime(2026, 3, 12),
        iconCodePoint: _iconAccountBalanceOutlined,
        iconFontFamily: _mi,
        connectionId: _connGtbank,
      ),
      SpendingTransaction(
        id: 'ng-t07',
        merchant: 'Lagos Water Corp',
        category: 'bills',
        subCategory: 'water',
        amountLabel: '-\u20A68,000.00',
        amountMajor: '8,000',
        amountMinor: '.00',
        currencySymbol: '\u20A6',
        isCredit: false,
        date: DateTime(2026, 3, 11),
        iconText: 'LW',
        connectionId: _connGtbank,
      ),
      SpendingTransaction(
        id: 'ng-t08',
        merchant: 'Jumia',
        category: 'shopping',
        subCategory: 'online',
        amountLabel: '-\u20A625,400.00',
        amountMajor: '25,400',
        amountMinor: '.00',
        currencySymbol: '\u20A6',
        isCredit: false,
        date: DateTime(2026, 3, 10),
        iconText: 'J',
        connectionId: _connGtbank,
      ),
      SpendingTransaction(
        id: 'ng-t09',
        merchant: 'DStv',
        category: 'subscriptions',
        subCategory: 'streaming',
        amountLabel: '-\u20A621,000.00',
        amountMajor: '21,000',
        amountMinor: '.00',
        currencySymbol: '\u20A6',
        isCredit: false,
        date: DateTime(2026, 3, 9),
        iconText: 'DS',
        connectionId: _connGtbank,
      ),
      SpendingTransaction(
        id: 'ng-t10',
        merchant: 'Mama Put',
        category: 'eating_out',
        subCategory: 'restaurant',
        amountLabel: '-\u20A62,500.00',
        amountMajor: '2,500',
        amountMinor: '.00',
        currencySymbol: '\u20A6',
        isCredit: false,
        date: DateTime(2026, 3, 8),
        iconText: 'MP',
        connectionId: _connGtbank,
      ),
      SpendingTransaction(
        id: 'ng-t11',
        merchant: 'Uber',
        category: 'transport',
        subCategory: 'ride_hailing',
        amountLabel: '-\u20A63,800.00',
        amountMajor: '3,800',
        amountMinor: '.00',
        currencySymbol: '\u20A6',
        isCredit: false,
        date: DateTime(2026, 3, 7),
        iconText: 'U',
        connectionId: _connGtbank,
      ),
      SpendingTransaction(
        id: 'ng-t12',
        merchant: 'Total Fuel',
        category: 'transport',
        subCategory: 'fuel',
        amountLabel: '-\u20A615,000.00',
        amountMajor: '15,000',
        amountMinor: '.00',
        currencySymbol: '\u20A6',
        isCredit: false,
        date: DateTime(2026, 3, 6),
        iconText: 'TF',
        connectionId: _connGtbank,
      ),
    ],
    // ── Nigeria Savings (2 transactions) ──
    'ng-savings': <SpendingTransaction>[
      SpendingTransaction(
        id: 'ngs-t01',
        merchant: 'Auto-save',
        category: 'savings',
        subCategory: 'goal_savings',
        amountLabel: '+\u20A650,000.00',
        amountMajor: '50,000',
        amountMinor: '.00',
        currencySymbol: '\u20A6',
        isCredit: true,
        date: DateTime(2026, 3, 13),
        iconCodePoint: _iconSavingsOutlined,
        iconFontFamily: _mi,
        connectionId: _connKuda,
      ),
      SpendingTransaction(
        id: 'ngs-t02',
        merchant: 'Kuda Interest',
        category: 'income',
        subCategory: 'interest',
        amountLabel: '+\u20A61,250.00',
        amountMajor: '1,250',
        amountMinor: '.00',
        currencySymbol: '\u20A6',
        isCredit: true,
        date: DateTime(2026, 3, 1),
        iconCodePoint: _iconSavingsOutlined,
        iconFontFamily: _mi,
        connectionId: _connKuda,
      ),
    ],
    // ── Dollar Domiciliary (2 transactions) ──
    'ng-domiciliary': <SpendingTransaction>[
      SpendingTransaction(
        id: 'ngd-t01',
        merchant: 'Freelance Client (USD)',
        category: 'income',
        subCategory: 'freelance',
        amountLabel: '+\$1,200.00',
        amountMajor: '1,200',
        amountMinor: '.00',
        currencySymbol: '\$',
        isCredit: true,
        date: DateTime(2026, 3, 10),
        iconCodePoint: _iconCurrencyExchange,
        iconFontFamily: _mi,
        connectionId: _connAccess,
      ),
      SpendingTransaction(
        id: 'ngd-t02',
        merchant: 'FX Conversion to NGN',
        category: 'transfer_out',
        subCategory: 'own_account',
        amountLabel: '-\$500.00',
        amountMajor: '500',
        amountMinor: '.00',
        currencySymbol: '\$',
        isCredit: false,
        date: DateTime(2026, 3, 8),
        iconCodePoint: _iconCurrencyExchange,
        iconFontFamily: _mi,
        connectionId: _connAccess,
      ),
    ],
  };

  // ─────────────────────────────────────────────────────────
  //  Synthetic transaction templates per currency
  //
  //  When an account is linked via open-banking, the user expects to see
  //  transactions from the bank. Manual accounts correctly show empty. These
  //  templates are combined with the account's currency symbol and connection
  //  ID at query time.
  // ─────────────────────────────────────────────────────────

  /// Template for generating a synthetic transaction. Currency-dependent
  /// fields (amountLabel, currencySymbol, connectionId) are filled at runtime.
  static const List<_SyntheticTxTemplate> _gbpTemplates =
      <_SyntheticTxTemplate>[
    _SyntheticTxTemplate(
      idSuffix: 'syn-01',
      merchant: 'Salary Credit',
      category: 'income',
      subCategory: 'salary',
      amountMajor: '3,200',
      amountMinor: '.00',
      isCredit: true,
      daysAgo: 2,
      iconCodePoint: _iconAccountBalanceOutlined,
      iconFontFamily: _mi,
    ),
    _SyntheticTxTemplate(
      idSuffix: 'syn-02',
      merchant: 'Tesco',
      category: 'groceries',
      subCategory: 'supermarket',
      amountMajor: '47',
      amountMinor: '.85',
      isCredit: false,
      daysAgo: 3,
      iconText: 'T',
    ),
    _SyntheticTxTemplate(
      idSuffix: 'syn-03',
      merchant: 'TfL',
      category: 'transport',
      subCategory: 'public_transit',
      amountMajor: '8',
      amountMinor: '.60',
      isCredit: false,
      daysAgo: 4,
      iconText: 'TL',
    ),
    _SyntheticTxTemplate(
      idSuffix: 'syn-04',
      merchant: 'Netflix',
      category: 'subscriptions',
      subCategory: 'streaming',
      amountMajor: '15',
      amountMinor: '.99',
      isCredit: false,
      daysAgo: 6,
      iconText: 'N',
    ),
    _SyntheticTxTemplate(
      idSuffix: 'syn-05',
      merchant: 'Costa Coffee',
      category: 'eating_out',
      subCategory: 'cafe',
      amountMajor: '4',
      amountMinor: '.50',
      isCredit: false,
      daysAgo: 7,
      iconText: 'CC',
    ),
  ];

  static const List<_SyntheticTxTemplate> _ngnTemplates =
      <_SyntheticTxTemplate>[
    _SyntheticTxTemplate(
      idSuffix: 'syn-01',
      merchant: 'Salary Credit',
      category: 'income',
      subCategory: 'salary',
      amountMajor: '450,000',
      amountMinor: '.00',
      isCredit: true,
      daysAgo: 2,
      iconCodePoint: _iconAccountBalanceOutlined,
      iconFontFamily: _mi,
    ),
    _SyntheticTxTemplate(
      idSuffix: 'syn-02',
      merchant: 'Shoprite',
      category: 'groceries',
      subCategory: 'supermarket',
      amountMajor: '12,800',
      amountMinor: '.00',
      isCredit: false,
      daysAgo: 3,
      iconText: 'SR',
    ),
    _SyntheticTxTemplate(
      idSuffix: 'syn-03',
      merchant: 'Bolt',
      category: 'transport',
      subCategory: 'ride_hailing',
      amountMajor: '3,500',
      amountMinor: '.00',
      isCredit: false,
      daysAgo: 5,
      iconText: 'BT',
    ),
    _SyntheticTxTemplate(
      idSuffix: 'syn-04',
      merchant: 'MTN Data',
      category: 'bills',
      subCategory: 'internet',
      amountMajor: '2,000',
      amountMinor: '.00',
      isCredit: false,
      daysAgo: 6,
      iconText: 'MT',
    ),
    _SyntheticTxTemplate(
      idSuffix: 'syn-05',
      merchant: 'Chicken Republic',
      category: 'eating_out',
      subCategory: 'fast_food',
      amountMajor: '4,200',
      amountMinor: '.00',
      isCredit: false,
      daysAgo: 7,
      iconText: 'CR',
    ),
  ];

  static const List<_SyntheticTxTemplate> _usdTemplates =
      <_SyntheticTxTemplate>[
    _SyntheticTxTemplate(
      idSuffix: 'syn-01',
      merchant: 'Wire Transfer In',
      category: 'income',
      subCategory: 'freelance',
      amountMajor: '2,500',
      amountMinor: '.00',
      isCredit: true,
      daysAgo: 3,
      iconCodePoint: _iconAccountBalanceOutlined,
      iconFontFamily: _mi,
    ),
    _SyntheticTxTemplate(
      idSuffix: 'syn-02',
      merchant: 'Amazon',
      category: 'shopping',
      subCategory: 'online',
      amountMajor: '34',
      amountMinor: '.99',
      isCredit: false,
      daysAgo: 4,
      iconText: 'a',
    ),
    _SyntheticTxTemplate(
      idSuffix: 'syn-03',
      merchant: 'Uber',
      category: 'transport',
      subCategory: 'ride_hailing',
      amountMajor: '18',
      amountMinor: '.40',
      isCredit: false,
      daysAgo: 5,
      iconText: 'U',
    ),
    _SyntheticTxTemplate(
      idSuffix: 'syn-04',
      merchant: 'Starbucks',
      category: 'eating_out',
      subCategory: 'cafe',
      amountMajor: '6',
      amountMinor: '.25',
      isCredit: false,
      daysAgo: 6,
      iconText: 'SB',
    ),
    _SyntheticTxTemplate(
      idSuffix: 'syn-05',
      merchant: 'Con Edison',
      category: 'bills',
      subCategory: 'electricity',
      amountMajor: '95',
      amountMinor: '.00',
      isCredit: false,
      daysAgo: 8,
      iconText: 'CE',
    ),
  ];

  /// Fallback templates used when the linked account's currency has no
  /// specific template set. Uses generic GBP-style amounts.
  static const List<_SyntheticTxTemplate> _defaultTemplates = _gbpTemplates;

  /// Selects the correct template list for a currency code.
  static List<_SyntheticTxTemplate> _templatesForCurrency(String code) {
    switch (code.toUpperCase()) {
      case 'GBP':
        return _gbpTemplates;
      case 'NGN':
        return _ngnTemplates;
      case 'USD':
        return _usdTemplates;
      default:
        return _defaultTemplates;
    }
  }

  /// Generates synthetic transactions for a runtime-linked account. Returns
  /// an empty list for manual accounts (correct: user hasn't imported data).
  List<SpendingTransaction> _synthesiseTransactions(
    AccountLinkItem account,
  ) {
    if (account.source != AccountLinkSource.linked) {
      return const <SpendingTransaction>[];
    }

    final String symbol = _currencySymbolFromCode(account.currencyCode);
    final List<_SyntheticTxTemplate> templates =
        _templatesForCurrency(account.currencyCode);
    final DateTime now = DateTime.now();

    return templates.map((_SyntheticTxTemplate t) {
      final String sign = t.isCredit ? '+' : '-';
      return SpendingTransaction(
        id: '${account.id}-${t.idSuffix}',
        merchant: t.merchant,
        category: t.category,
        subCategory: t.subCategory,
        amountLabel: '$sign$symbol${t.amountMajor}${t.amountMinor}',
        amountMajor: t.amountMajor,
        amountMinor: t.amountMinor,
        currencySymbol: symbol,
        isCredit: t.isCredit,
        date: now.subtract(Duration(days: t.daysAgo)),
        iconText: t.iconText,
        iconCodePoint: t.iconCodePoint,
        iconFontFamily: t.iconFontFamily,
        connectionId: account.connectionId,
      );
    }).toList();
  }

  // ─────────────────────────────────────────────────────────
  //  getTransactions
  // ─────────────────────────────────────────────────────────

  @override
  Future<List<SpendingTransaction>> getTransactions(String accountId) async {
    await MockBehavior.delay();
    MockBehavior.throwIfEnabled('spending.getTransactions');

    // Seed transactions exist only in populated mode.
    final List<SpendingTransaction>? seedTxns = _transactions[accountId];

    if (seedTxns != null && seedTxns.isNotEmpty) {
      // Seed account — filter by active connections and return.
      return seedTxns
          .where(
              (SpendingTransaction t) => _isConnectionActive(t.connectionId))
          .toList();
    }

    // No seed data for this account. Check if it's a runtime-linked account
    // that should have synthesised transactions.
    final List<AccountLinkItem> runtimeAccounts =
        _runtimeAccountsGetter?.call() ?? const <AccountLinkItem>[];

    for (final AccountLinkItem account in runtimeAccounts) {
      if (account.id != accountId) continue;

      // Only synthesise for linked accounts; manual accounts correctly
      // show empty (the user created a blank tracking account).
      if (account.source == AccountLinkSource.linked &&
          _isConnectionActive(account.connectionId)) {
        return _synthesiseTransactions(account);
      }
      break;
    }

    // Fresh mode or manual account — return any manually-added transactions.
    final List<SpendingTransaction> manual =
        _manualTransactions[accountId] ?? const <SpendingTransaction>[];
    return List<SpendingTransaction>.of(manual);
  }

  // ─────────────────────────────────────────────────────────
  //  addTransaction
  // ─────────────────────────────────────────────────────────

  @override
  Future<SpendingTransaction> addTransaction(
    String accountId,
    CreateTransactionRequest request,
  ) async {
    await MockBehavior.delay();
    MockBehavior.throwIfEnabled('spending.addTransaction');

    _manualTxCounter++;
    final String txId = 'manual-tx-$_manualTxCounter';
    final String symbol = _currencySymbolFromCode(request.currency);

    // Split decimal amount into major/minor string parts.
    final String amountStr = request.amount.toStringAsFixed(2);
    final int dotIndex = amountStr.indexOf('.');
    final String amountMajor =
        dotIndex >= 0 ? amountStr.substring(0, dotIndex) : amountStr;
    final String amountMinor =
        dotIndex >= 0 ? amountStr.substring(dotIndex) : '.00';

    final String sign = request.isCredit ? '+' : '-';
    final String amountLabel = '$sign$symbol$amountMajor$amountMinor';

    // First letter(s) of merchant as icon text.
    final String iconText = request.merchant.isNotEmpty
        ? request.merchant.substring(
            0,
            request.merchant.length >= 2 ? 2 : 1,
          ).toUpperCase()
        : '?';

    final SpendingTransaction transaction = SpendingTransaction(
      id: txId,
      merchant: request.merchant,
      category: request.category,
      amountLabel: amountLabel,
      amountMajor: amountMajor,
      amountMinor: amountMinor,
      currencySymbol: symbol,
      isCredit: request.isCredit,
      date: request.date,
      iconText: iconText,
      notes: request.notes,
    );

    _manualTransactions
        .putIfAbsent(accountId, () => <SpendingTransaction>[])
        .insert(0, transaction);

    return transaction;
  }

  // ─────────────────────────────────────────────────────────
  //  Seed data — overview
  // ─────────────────────────────────────────────────────────

  static const List<SpendingAccountSnapshot> _seedOverviewSnapshots =
      <SpendingAccountSnapshot>[
    SpendingAccountSnapshot(
      label: 'UK Current',
      balanceLabel: '\u00A33,842.16',
      statusLabel: 'Primary',
      changeLabel: '+\u00A3186.40 this week',
      gradientKey: 'primary',
      iconCodePoint: _iconWalletOutlined,
      iconFontFamily: _mi,
      connectionId: _connStarling,
    ),
    SpendingAccountSnapshot(
      label: 'UK Savings',
      balanceLabel: '\u00A36,240.00',
      statusLabel: 'Savings',
      changeLabel: '+\u00A3120.00 auto-saved',
      gradientKey: 'savings',
      iconCodePoint: _iconSavingsOutlined,
      iconFontFamily: _mi,
      connectionId: _connStarling,
    ),
    SpendingAccountSnapshot(
      label: 'Bills card',
      balanceLabel: '-\u00A3842.30',
      statusLabel: 'Credit',
      changeLabel: 'Payment due 28 Mar',
      gradientKey: 'bills',
      iconCodePoint: _iconCreditCardOutlined,
      iconFontFamily: _mi,
      connectionId: _connAmex,
    ),
    SpendingAccountSnapshot(
      label: 'Naira Current',
      balanceLabel: '\u20A6485,200',
      statusLabel: 'Nigeria',
      changeLabel: '+\u20A632,500 this week',
      gradientKey: 'primary',
      iconCodePoint: _iconAccountBalanceOutlined,
      iconFontFamily: _mi,
      connectionId: _connGtbank,
    ),
    SpendingAccountSnapshot(
      label: 'Naira Savings',
      balanceLabel: '\u20A61,240,000',
      statusLabel: 'Savings',
      changeLabel: '+\u20A650,000 auto-saved',
      gradientKey: 'savings',
      iconCodePoint: _iconSavingsOutlined,
      iconFontFamily: _mi,
      connectionId: _connKuda,
    ),
    SpendingAccountSnapshot(
      label: 'Dollar Dom.',
      balanceLabel: '\$2,150.00',
      statusLabel: 'Domiciliary',
      changeLabel: '+\$1,200 freelance',
      gradientKey: 'bills',
      iconCodePoint: _iconCurrencyExchange,
      iconFontFamily: _mi,
      connectionId: _connAccess,
    ),
  ];

  // ─────────────────────────────────────────────────────────
  //  getOverview
  // ─────────────────────────────────────────────────────────

  static const SpendingOverviewData _freshOverview = SpendingOverviewData(
    accountSnapshots: <SpendingAccountSnapshot>[],
    totalBalanceMetric: SpendingMetric(
      label: 'Total balance',
      amountLabel: '\u00A30.00',
      trendLabel: '',
      iconCodePoint: _iconStackedLineChart,
      iconFontFamily: _mi,
    ),
    netWorthMetric: SpendingMetric(
      label: 'Net worth',
      amountLabel: '\u00A30.00',
      trendLabel: '',
      iconCodePoint: _iconDiamondOutlined,
      iconFontFamily: _mi,
    ),
    safeToSpendLabel: '\u00A30.00',
    safeToSpendSubtitle: '',
    breakdownSlices: <SpendingBreakdownSlice>[],
    breakdownTotalLabel: '\u00A30',
    trendSummaryLabel: '',
    trendSpots: <SpendingTrendSpot>[],
    trendBottomLabels: <String>[],
    insightTitle: '',
    insightBody: '',
    allocationSlices: <SpendingAllocationSlice>[],
    allocationMonthLabel: '',
    allocationYearLabel: '',
    allocationChipLabel: '',
    recentTransactions: <SpendingRecentTransaction>[],
  );

  @override
  Future<SpendingOverviewData> getOverview() async {
    await MockBehavior.delay();
    MockBehavior.throwIfEnabled('spending.getOverview');

    // Filter overview snapshots by active connections.
    final List<SpendingAccountSnapshot> filteredSnapshots =
        demoDataMode == DemoDataMode.fresh
            ? <SpendingAccountSnapshot>[]
            : _overviewSnapshots
                .where((SpendingAccountSnapshot s) =>
                    _isConnectionActive(s.connectionId))
                .toList();

    // Append snapshots for runtime-created accounts (linked or manual) that
    // have no seed representation.
    final List<AccountLinkItem> runtimeAccounts =
        _runtimeAccountsGetter?.call() ?? const <AccountLinkItem>[];
    final Set<String> existingLabels =
        filteredSnapshots.map((SpendingAccountSnapshot s) => s.label).toSet();

    for (final AccountLinkItem item in runtimeAccounts) {
      if (existingLabels.contains(item.name)) continue;

      final String symbol = _currencySymbolFromCode(item.currencyCode);
      final bool isManual = item.source == AccountLinkSource.manual;
      final int iconCodePoint =
          isManual ? _iconEditOutlined : _iconAccountBalanceOutlined;
      final String statusLabel = isManual ? 'Manual' : 'Linked';
      final String changeLabel =
          isManual ? 'Manually tracked' : 'Recently linked';

      filteredSnapshots.add(
        SpendingAccountSnapshot(
          label: item.name,
          balanceLabel: item.balanceLabel ?? '${symbol}0.00',
          statusLabel: statusLabel,
          changeLabel: changeLabel,
          gradientKey: 'primary',
          iconCodePoint: iconCodePoint,
          iconFontFamily: _mi,
          connectionId: item.connectionId,
        ),
      );
    }

    // If no accounts exist at all, return an empty overview.
    if (filteredSnapshots.isEmpty) {
      return _freshOverview;
    }

    return SpendingOverviewData(
      accountSnapshots: filteredSnapshots,
      totalBalanceMetric: const SpendingMetric(
        label: 'Total balance',
        amountLabel: '\u00A314,826.46',
        trendLabel: '+4.6% vs last month',
        iconCodePoint: _iconStackedLineChart,
        iconFontFamily: _mi,
      ),
      netWorthMetric: const SpendingMetric(
        label: 'Net worth',
        amountLabel: '\u00A322,180.64',
        trendLabel: '+\u00A3920 this month',
        iconCodePoint: _iconDiamondOutlined,
        iconFontFamily: _mi,
      ),
      safeToSpendLabel: '\u00A3820.00',
      safeToSpendSubtitle:
          'After bills, goals, and your weekly safety buffer.',
      breakdownSlices: const <SpendingBreakdownSlice>[
        SpendingBreakdownSlice(
          label: 'Food',
          amountLabel: '\u00A3570',
          value: 31,
          colorKey: 'primary',
        ),
        SpendingBreakdownSlice(
          label: 'Bills',
          amountLabel: '\u00A3410',
          value: 22,
          colorKey: 'bills',
        ),
        SpendingBreakdownSlice(
          label: 'Transport',
          amountLabel: '\u00A3312',
          value: 17,
          colorKey: 'success',
        ),
        SpendingBreakdownSlice(
          label: 'Shopping',
          amountLabel: '\u00A3260',
          value: 14,
          colorKey: 'info',
        ),
        SpendingBreakdownSlice(
          label: 'Other',
          amountLabel: '\u00A3288',
          value: 16,
          colorKey: 'other',
        ),
      ],
      breakdownTotalLabel: '\u00A31,840',
      trendSummaryLabel: 'Spend is tracking 6% lower than last month.',
      trendSpots: const <SpendingTrendSpot>[
        SpendingTrendSpot(x: 0, y: 360),
        SpendingTrendSpot(x: 1, y: 410),
        SpendingTrendSpot(x: 2, y: 325),
        SpendingTrendSpot(x: 3, y: 298),
        SpendingTrendSpot(x: 4, y: 340),
      ],
      trendBottomLabels: const <String>['W1', 'W2', 'W3', 'W4', 'Now'],
      insightTitle:
          'Your food spending is 12% higher than usual this week.',
      insightBody:
          'Most of the lift came from weekday deliveries after 8pm.',
      allocationSlices: const <SpendingAllocationSlice>[
        SpendingAllocationSlice(
          label: 'Income',
          amountLabel: '\u00A34,232.24',
          value: 4232.24,
          colorKey: 'success',
        ),
        SpendingAllocationSlice(
          label: 'Expenses',
          amountLabel: '\u00A32,660.12',
          value: 2660.12,
          colorKey: 'primary',
        ),
        SpendingAllocationSlice(
          label: 'Investments',
          amountLabel: '\u00A31,754.64',
          value: 1754.64,
          colorKey: 'info',
        ),
      ],
      allocationMonthLabel: 'March',
      allocationYearLabel: '2026',
      allocationChipLabel: 'Mar',
      recentTransactions: const <SpendingRecentTransaction>[
        SpendingRecentTransaction(
          merchant: 'Uber',
          category: 'transport',
          subCategory: 'ride_hailing',
          amountLabel: '\u00A314.20',
          iconText: 'U',
          iconBackgroundKey: 'dark',
          iconForegroundKey: 'surfaceBase',
        ),
        SpendingRecentTransaction(
          merchant: 'Amazon',
          category: 'shopping',
          subCategory: 'online',
          amountLabel: '\u00A327.99',
          iconText: 'a',
          iconBackgroundKey: 'warmSurface',
          iconForegroundKey: 'dark',
        ),
        SpendingRecentTransaction(
          merchant: "Nando's",
          category: 'eating_out',
          subCategory: 'restaurant',
          amountLabel: '\u00A328.45',
          iconText: 'N',
          iconBackgroundKey: 'warmAccent',
          iconForegroundKey: 'warmText',
        ),
        SpendingRecentTransaction(
          merchant: 'Shoprite Lekki',
          category: 'groceries',
          subCategory: 'supermarket',
          amountLabel: '\u20A618,500',
          iconText: 'SR',
          iconBackgroundKey: 'dark',
          iconForegroundKey: 'surfaceBase',
        ),
        SpendingRecentTransaction(
          merchant: 'Eko Electricity',
          category: 'bills',
          subCategory: 'electricity',
          amountLabel: '\u20A612,000',
          iconText: 'EE',
          iconBackgroundKey: 'warmSurface',
          iconForegroundKey: 'dark',
        ),
      ],
    );
  }

  // ─────────────────────────────────────────────────────────
  //  getMerchantHistory
  // ─────────────────────────────────────────────────────────

  static const Map<String, SpendingMerchantHistory> _merchantHistories =
      <String, SpendingMerchantHistory>{
    'Tesco': SpendingMerchantHistory(
      transactionCountLabel: '42',
      averageSpendLabel: '\u00A351.20',
      totalSpentLabel: '\u00A32,150.40',
    ),
    'Uber': SpendingMerchantHistory(
      transactionCountLabel: '67',
      averageSpendLabel: '\u00A312.80',
      totalSpentLabel: '\u00A3857.60',
    ),
    'Amazon': SpendingMerchantHistory(
      transactionCountLabel: '28',
      averageSpendLabel: '\u00A334.50',
      totalSpentLabel: '\u00A3966.00',
    ),
    'Netflix': SpendingMerchantHistory(
      transactionCountLabel: '12',
      averageSpendLabel: '\u00A315.99',
      totalSpentLabel: '\u00A3191.88',
    ),
    'Shoprite Lekki': SpendingMerchantHistory(
      transactionCountLabel: '24',
      averageSpendLabel: '\u20A616,200',
      totalSpentLabel: '\u20A6388,800',
    ),
    'Eko Electricity': SpendingMerchantHistory(
      transactionCountLabel: '12',
      averageSpendLabel: '\u20A611,500',
      totalSpentLabel: '\u20A6138,000',
    ),
  };

  static const SpendingMerchantHistory _defaultHistory =
      SpendingMerchantHistory(
    transactionCountLabel: '18',
    averageSpendLabel: '\u00A326.97',
    totalSpentLabel: '\u00A3485.46',
  );

  @override
  Future<SpendingMerchantHistory> getMerchantHistory(
    String merchantName,
  ) async {
    await MockBehavior.delay();
    MockBehavior.throwIfEnabled('spending.getMerchantHistory');

    if (demoDataMode == DemoDataMode.fresh) {
      return const SpendingMerchantHistory(
        transactionCountLabel: '0',
        averageSpendLabel: '\u00A30.00',
        totalSpentLabel: '\u00A30.00',
      );
    }

    return _merchantHistories[merchantName] ?? _defaultHistory;
  }
}

// ─────────────────────────────────────────────────────────
//  Helper: synthetic transaction template
// ─────────────────────────────────────────────────────────

/// Immutable template for generating synthetic transactions for
/// runtime-linked accounts. Currency symbol and connection ID are
/// filled in at generation time by [MockSpendingRepository].
class _SyntheticTxTemplate {
  const _SyntheticTxTemplate({
    required this.idSuffix,
    required this.merchant,
    required this.category,
    this.subCategory,
    required this.amountMajor,
    required this.amountMinor,
    required this.isCredit,
    required this.daysAgo,
    this.iconText,
    this.iconCodePoint,
    this.iconFontFamily,
  });

  final String idSuffix;
  final String merchant;
  final String category;
  final String? subCategory;
  final String amountMajor;
  final String amountMinor;
  final bool isCredit;
  final int daysAgo;
  final String? iconText;
  final int? iconCodePoint;
  final String? iconFontFamily;
}
