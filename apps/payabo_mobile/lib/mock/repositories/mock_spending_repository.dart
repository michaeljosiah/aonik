import '../../app/demo/demo_data_mode.dart';
import '../../data/repositories/spending_repository.dart';
import '../mock_behavior.dart';

class MockSpendingRepository implements SpendingRepository {
  MockSpendingRepository({
    this.demoDataMode = DemoDataMode.populated,
  });

  final DemoDataMode demoDataMode;

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
  // Icons.receipt_long_outlined
  static const int _iconReceiptLongOutlined = 0xf2ef;
  // Icons.stacked_line_chart
  static const int _iconStackedLineChart = 0xe5f7;
  // Icons.diamond_outlined
  static const int _iconDiamondOutlined = 0xf05e7;
  // Icons.account_balance_outlined
  static const int _iconAccountBalanceOutlined = 0xee2f;
  // Icons.currency_exchange
  static const int _iconCurrencyExchange = 0xf05b4;

  // ─────────────────────────────────────────────────────────
  //  getAccounts
  // ─────────────────────────────────────────────────────────

  static const List<SpendingAccountCard> _accounts = <SpendingAccountCard>[
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
    ),
  ];

  @override
  Future<List<SpendingAccountCard>> getAccounts() async {
    await MockBehavior.delay();
    MockBehavior.throwIfEnabled('spending.getAccounts');

    if (demoDataMode == DemoDataMode.fresh) {
      return const <SpendingAccountCard>[];
    }

    return _accounts;
  }

  // ─────────────────────────────────────────────────────────
  //  getTransactions
  // ─────────────────────────────────────────────────────────

  static final Map<String, List<SpendingTransaction>> _transactions =
      <String, List<SpendingTransaction>>{
    // ── UK Current (15 transactions) ──
    'uk-current': <SpendingTransaction>[
      SpendingTransaction(
        id: 'uk-t01',
        merchant: 'Open Rent',
        category: 'Housing',
        amountLabel: '+\u00A31,450.00',
        amountMajor: '1,450',
        amountMinor: '.00',
        currencySymbol: '\u00A3',
        isCredit: true,
        date: DateTime(2026, 3, 17),
        iconCodePoint: _iconHomeOutlined,
        iconFontFamily: _mi,
      ),
      SpendingTransaction(
        id: 'uk-t02',
        merchant: 'Tesco',
        category: 'Groceries',
        amountLabel: '-\u00A354.12',
        amountMajor: '54',
        amountMinor: '.12',
        currencySymbol: '\u00A3',
        isCredit: false,
        date: DateTime(2026, 3, 16),
        iconText: 'T',
      ),
      SpendingTransaction(
        id: 'uk-t03',
        merchant: 'Uber',
        category: 'Transport',
        amountLabel: '-\u00A314.20',
        amountMajor: '14',
        amountMinor: '.20',
        currencySymbol: '\u00A3',
        isCredit: false,
        date: DateTime(2026, 3, 16),
        iconText: 'U',
      ),
      SpendingTransaction(
        id: 'uk-t04',
        merchant: 'Amazon',
        category: 'Shopping',
        amountLabel: '-\u00A327.99',
        amountMajor: '27',
        amountMinor: '.99',
        currencySymbol: '\u00A3',
        isCredit: false,
        date: DateTime(2026, 3, 15),
        iconText: 'a',
      ),
      SpendingTransaction(
        id: 'uk-t05',
        merchant: "Nando's",
        category: 'Dining',
        amountLabel: '-\u00A328.45',
        amountMajor: '28',
        amountMinor: '.45',
        currencySymbol: '\u00A3',
        isCredit: false,
        date: DateTime(2026, 3, 14),
        iconText: 'N',
      ),
      SpendingTransaction(
        id: 'uk-t06',
        merchant: 'TfL',
        category: 'Transport',
        amountLabel: '-\u00A37.40',
        amountMajor: '7',
        amountMinor: '.40',
        currencySymbol: '\u00A3',
        isCredit: false,
        date: DateTime(2026, 3, 13),
        iconText: 'TL',
      ),
      SpendingTransaction(
        id: 'uk-t07',
        merchant: 'Sainsbury\'s',
        category: 'Groceries',
        amountLabel: '-\u00A362.30',
        amountMajor: '62',
        amountMinor: '.30',
        currencySymbol: '\u00A3',
        isCredit: false,
        date: DateTime(2026, 3, 12),
        iconText: 'S',
      ),
      SpendingTransaction(
        id: 'uk-t08',
        merchant: 'Shell',
        category: 'Transport',
        amountLabel: '-\u00A358.40',
        amountMajor: '58',
        amountMinor: '.40',
        currencySymbol: '\u00A3',
        isCredit: false,
        date: DateTime(2026, 3, 11),
        iconText: 'SH',
      ),
      SpendingTransaction(
        id: 'uk-t09',
        merchant: 'Gym Group',
        category: 'Health',
        amountLabel: '-\u00A324.99',
        amountMajor: '24',
        amountMinor: '.99',
        currencySymbol: '\u00A3',
        isCredit: false,
        date: DateTime(2026, 3, 10),
        iconText: 'GG',
      ),
      SpendingTransaction(
        id: 'uk-t10',
        merchant: 'Boots',
        category: 'Health',
        amountLabel: '-\u00A312.50',
        amountMajor: '12',
        amountMinor: '.50',
        currencySymbol: '\u00A3',
        isCredit: false,
        date: DateTime(2026, 3, 9),
        iconText: 'B',
      ),
      SpendingTransaction(
        id: 'uk-t11',
        merchant: 'Deliveroo',
        category: 'Dining',
        amountLabel: '-\u00A319.80',
        amountMajor: '19',
        amountMinor: '.80',
        currencySymbol: '\u00A3',
        isCredit: false,
        date: DateTime(2026, 3, 8),
        iconText: 'D',
      ),
      SpendingTransaction(
        id: 'uk-t12',
        merchant: 'British Gas',
        category: 'Utilities',
        amountLabel: '-\u00A386.00',
        amountMajor: '86',
        amountMinor: '.00',
        currencySymbol: '\u00A3',
        isCredit: false,
        date: DateTime(2026, 3, 7),
        iconText: 'BG',
      ),
      SpendingTransaction(
        id: 'uk-t13',
        merchant: 'Pret A Manger',
        category: 'Dining',
        amountLabel: '-\u00A35.60',
        amountMajor: '5',
        amountMinor: '.60',
        currencySymbol: '\u00A3',
        isCredit: false,
        date: DateTime(2026, 3, 6),
        iconText: 'P',
      ),
      SpendingTransaction(
        id: 'uk-t14',
        merchant: 'Vodafone',
        category: 'Utilities',
        amountLabel: '-\u00A325.00',
        amountMajor: '25',
        amountMinor: '.00',
        currencySymbol: '\u00A3',
        isCredit: false,
        date: DateTime(2026, 3, 5),
        iconText: 'V',
      ),
      SpendingTransaction(
        id: 'uk-t15',
        merchant: 'John Lewis',
        category: 'Shopping',
        amountLabel: '-\u00A389.00',
        amountMajor: '89',
        amountMinor: '.00',
        currencySymbol: '\u00A3',
        isCredit: false,
        date: DateTime(2026, 3, 4),
        iconText: 'JL',
      ),
    ],
    // ── UK Savings (3 transactions) ──
    'uk-savings': <SpendingTransaction>[
      SpendingTransaction(
        id: 'uks-t01',
        merchant: 'Auto-save',
        category: 'Savings',
        amountLabel: '+\u00A3200.00',
        amountMajor: '200',
        amountMinor: '.00',
        currencySymbol: '\u00A3',
        isCredit: true,
        date: DateTime(2026, 3, 15),
        iconCodePoint: _iconSavingsOutlined,
        iconFontFamily: _mi,
      ),
      SpendingTransaction(
        id: 'uks-t02',
        merchant: 'Starling Interest',
        category: 'Income',
        amountLabel: '+\u00A34.80',
        amountMajor: '4',
        amountMinor: '.80',
        currencySymbol: '\u00A3',
        isCredit: true,
        date: DateTime(2026, 3, 1),
        iconCodePoint: _iconSavingsOutlined,
        iconFontFamily: _mi,
      ),
      SpendingTransaction(
        id: 'uks-t03',
        merchant: 'Auto-save',
        category: 'Savings',
        amountLabel: '+\u00A3200.00',
        amountMajor: '200',
        amountMinor: '.00',
        currencySymbol: '\u00A3',
        isCredit: true,
        date: DateTime(2026, 2, 15),
        iconCodePoint: _iconSavingsOutlined,
        iconFontFamily: _mi,
      ),
    ],
    // ── UK Credit Card (6 transactions) ──
    'uk-credit': <SpendingTransaction>[
      SpendingTransaction(
        id: 'ukc-t01',
        merchant: 'Netflix',
        category: 'Entertainment',
        amountLabel: '-\u00A315.99',
        amountMajor: '15',
        amountMinor: '.99',
        currencySymbol: '\u00A3',
        isCredit: false,
        date: DateTime(2026, 3, 15),
        iconText: 'N',
      ),
      SpendingTransaction(
        id: 'ukc-t02',
        merchant: 'Spotify',
        category: 'Entertainment',
        amountLabel: '-\u00A310.99',
        amountMajor: '10',
        amountMinor: '.99',
        currencySymbol: '\u00A3',
        isCredit: false,
        date: DateTime(2026, 3, 14),
        iconText: 'S',
      ),
      SpendingTransaction(
        id: 'ukc-t03',
        merchant: 'Apple iCloud',
        category: 'Entertainment',
        amountLabel: '-\u00A30.99',
        amountMajor: '0',
        amountMinor: '.99',
        currencySymbol: '\u00A3',
        isCredit: false,
        date: DateTime(2026, 3, 12),
        iconText: 'A',
      ),
      SpendingTransaction(
        id: 'ukc-t04',
        merchant: 'Amazon Prime',
        category: 'Entertainment',
        amountLabel: '-\u00A38.99',
        amountMajor: '8',
        amountMinor: '.99',
        currencySymbol: '\u00A3',
        isCredit: false,
        date: DateTime(2026, 3, 10),
        iconText: 'AP',
      ),
      SpendingTransaction(
        id: 'ukc-t05',
        merchant: 'ASOS',
        category: 'Shopping',
        amountLabel: '-\u00A345.60',
        amountMajor: '45',
        amountMinor: '.60',
        currencySymbol: '\u00A3',
        isCredit: false,
        date: DateTime(2026, 3, 8),
        iconText: 'AS',
      ),
      SpendingTransaction(
        id: 'ukc-t06',
        merchant: 'EasyJet',
        category: 'Travel',
        amountLabel: '-\u00A3189.00',
        amountMajor: '189',
        amountMinor: '.00',
        currencySymbol: '\u00A3',
        isCredit: false,
        date: DateTime(2026, 3, 5),
        iconText: 'EJ',
      ),
    ],
    // ── Nigeria Current (12 transactions) ──
    'ng-current': <SpendingTransaction>[
      SpendingTransaction(
        id: 'ng-t01',
        merchant: 'Shoprite Lekki',
        category: 'Groceries',
        amountLabel: '-\u20A618,500.00',
        amountMajor: '18,500',
        amountMinor: '.00',
        currencySymbol: '\u20A6',
        isCredit: false,
        date: DateTime(2026, 3, 17),
        iconText: 'SR',
      ),
      SpendingTransaction(
        id: 'ng-t02',
        merchant: 'Eko Electricity',
        category: 'Utilities',
        amountLabel: '-\u20A612,000.00',
        amountMajor: '12,000',
        amountMinor: '.00',
        currencySymbol: '\u20A6',
        isCredit: false,
        date: DateTime(2026, 3, 16),
        iconText: 'EE',
      ),
      SpendingTransaction(
        id: 'ng-t03',
        merchant: 'Bolt',
        category: 'Transport',
        amountLabel: '-\u20A64,200.00',
        amountMajor: '4,200',
        amountMinor: '.00',
        currencySymbol: '\u20A6',
        isCredit: false,
        date: DateTime(2026, 3, 15),
        iconText: 'BT',
      ),
      SpendingTransaction(
        id: 'ng-t04',
        merchant: 'MTN Data',
        category: 'Utilities',
        amountLabel: '-\u20A63,500.00',
        amountMajor: '3,500',
        amountMinor: '.00',
        currencySymbol: '\u20A6',
        isCredit: false,
        date: DateTime(2026, 3, 14),
        iconText: 'MT',
      ),
      SpendingTransaction(
        id: 'ng-t05',
        merchant: 'Chicken Republic',
        category: 'Dining',
        amountLabel: '-\u20A65,800.00',
        amountMajor: '5,800',
        amountMinor: '.00',
        currencySymbol: '\u20A6',
        isCredit: false,
        date: DateTime(2026, 3, 13),
        iconText: 'CR',
      ),
      SpendingTransaction(
        id: 'ng-t06',
        merchant: 'Salary Credit',
        category: 'Income',
        amountLabel: '+\u20A6750,000.00',
        amountMajor: '750,000',
        amountMinor: '.00',
        currencySymbol: '\u20A6',
        isCredit: true,
        date: DateTime(2026, 3, 12),
        iconCodePoint: _iconAccountBalanceOutlined,
        iconFontFamily: _mi,
      ),
      SpendingTransaction(
        id: 'ng-t07',
        merchant: 'Lagos Water Corp',
        category: 'Utilities',
        amountLabel: '-\u20A68,000.00',
        amountMajor: '8,000',
        amountMinor: '.00',
        currencySymbol: '\u20A6',
        isCredit: false,
        date: DateTime(2026, 3, 11),
        iconText: 'LW',
      ),
      SpendingTransaction(
        id: 'ng-t08',
        merchant: 'Jumia',
        category: 'Shopping',
        amountLabel: '-\u20A625,400.00',
        amountMajor: '25,400',
        amountMinor: '.00',
        currencySymbol: '\u20A6',
        isCredit: false,
        date: DateTime(2026, 3, 10),
        iconText: 'J',
      ),
      SpendingTransaction(
        id: 'ng-t09',
        merchant: 'DStv',
        category: 'Entertainment',
        amountLabel: '-\u20A621,000.00',
        amountMajor: '21,000',
        amountMinor: '.00',
        currencySymbol: '\u20A6',
        isCredit: false,
        date: DateTime(2026, 3, 9),
        iconText: 'DS',
      ),
      SpendingTransaction(
        id: 'ng-t10',
        merchant: 'Mama Put',
        category: 'Dining',
        amountLabel: '-\u20A62,500.00',
        amountMajor: '2,500',
        amountMinor: '.00',
        currencySymbol: '\u20A6',
        isCredit: false,
        date: DateTime(2026, 3, 8),
        iconText: 'MP',
      ),
      SpendingTransaction(
        id: 'ng-t11',
        merchant: 'Uber',
        category: 'Transport',
        amountLabel: '-\u20A63,800.00',
        amountMajor: '3,800',
        amountMinor: '.00',
        currencySymbol: '\u20A6',
        isCredit: false,
        date: DateTime(2026, 3, 7),
        iconText: 'U',
      ),
      SpendingTransaction(
        id: 'ng-t12',
        merchant: 'Total Fuel',
        category: 'Transport',
        amountLabel: '-\u20A615,000.00',
        amountMajor: '15,000',
        amountMinor: '.00',
        currencySymbol: '\u20A6',
        isCredit: false,
        date: DateTime(2026, 3, 6),
        iconText: 'TF',
      ),
    ],
    // ── Nigeria Savings (2 transactions) ──
    'ng-savings': <SpendingTransaction>[
      SpendingTransaction(
        id: 'ngs-t01',
        merchant: 'Auto-save',
        category: 'Savings',
        amountLabel: '+\u20A650,000.00',
        amountMajor: '50,000',
        amountMinor: '.00',
        currencySymbol: '\u20A6',
        isCredit: true,
        date: DateTime(2026, 3, 13),
        iconCodePoint: _iconSavingsOutlined,
        iconFontFamily: _mi,
      ),
      SpendingTransaction(
        id: 'ngs-t02',
        merchant: 'Kuda Interest',
        category: 'Income',
        amountLabel: '+\u20A61,250.00',
        amountMajor: '1,250',
        amountMinor: '.00',
        currencySymbol: '\u20A6',
        isCredit: true,
        date: DateTime(2026, 3, 1),
        iconCodePoint: _iconSavingsOutlined,
        iconFontFamily: _mi,
      ),
    ],
    // ── Dollar Domiciliary (2 transactions) ──
    'ng-domiciliary': <SpendingTransaction>[
      SpendingTransaction(
        id: 'ngd-t01',
        merchant: 'Freelance Client (USD)',
        category: 'Income',
        amountLabel: '+\$1,200.00',
        amountMajor: '1,200',
        amountMinor: '.00',
        currencySymbol: '\$',
        isCredit: true,
        date: DateTime(2026, 3, 10),
        iconCodePoint: _iconCurrencyExchange,
        iconFontFamily: _mi,
      ),
      SpendingTransaction(
        id: 'ngd-t02',
        merchant: 'FX Conversion to NGN',
        category: 'Transfer',
        amountLabel: '-\$500.00',
        amountMajor: '500',
        amountMinor: '.00',
        currencySymbol: '\$',
        isCredit: false,
        date: DateTime(2026, 3, 8),
        iconCodePoint: _iconCurrencyExchange,
        iconFontFamily: _mi,
      ),
    ],
  };

  @override
  Future<List<SpendingTransaction>> getTransactions(String accountId) async {
    await MockBehavior.delay();
    MockBehavior.throwIfEnabled('spending.getTransactions');

    if (demoDataMode == DemoDataMode.fresh) {
      return const <SpendingTransaction>[];
    }

    return _transactions[accountId] ?? const <SpendingTransaction>[];
  }

  // ─────────────────────────────────────────────────────────
  //  getOverview
  // ─────────────────────────────────────────────────────────

  static const SpendingOverviewData _populatedOverview = SpendingOverviewData(
    accountSnapshots: <SpendingAccountSnapshot>[
      SpendingAccountSnapshot(
        label: 'UK Current',
        balanceLabel: '\u00A33,842.16',
        statusLabel: 'Primary',
        changeLabel: '+\u00A3186.40 this week',
        gradientKey: 'primary',
        iconCodePoint: _iconWalletOutlined,
        iconFontFamily: _mi,
      ),
      SpendingAccountSnapshot(
        label: 'UK Savings',
        balanceLabel: '\u00A36,240.00',
        statusLabel: 'Savings',
        changeLabel: '+\u00A3120.00 auto-saved',
        gradientKey: 'savings',
        iconCodePoint: _iconSavingsOutlined,
        iconFontFamily: _mi,
      ),
      SpendingAccountSnapshot(
        label: 'Bills card',
        balanceLabel: '-\u00A3842.30',
        statusLabel: 'Credit',
        changeLabel: 'Payment due 28 Mar',
        gradientKey: 'bills',
        iconCodePoint: _iconCreditCardOutlined,
        iconFontFamily: _mi,
      ),
      SpendingAccountSnapshot(
        label: 'Naira Current',
        balanceLabel: '\u20A6485,200',
        statusLabel: 'Nigeria',
        changeLabel: '+\u20A632,500 this week',
        gradientKey: 'primary',
        iconCodePoint: _iconAccountBalanceOutlined,
        iconFontFamily: _mi,
      ),
      SpendingAccountSnapshot(
        label: 'Naira Savings',
        balanceLabel: '\u20A61,240,000',
        statusLabel: 'Savings',
        changeLabel: '+\u20A650,000 auto-saved',
        gradientKey: 'savings',
        iconCodePoint: _iconSavingsOutlined,
        iconFontFamily: _mi,
      ),
      SpendingAccountSnapshot(
        label: 'Dollar Dom.',
        balanceLabel: '\$2,150.00',
        statusLabel: 'Domiciliary',
        changeLabel: '+\$1,200 freelance',
        gradientKey: 'bills',
        iconCodePoint: _iconCurrencyExchange,
        iconFontFamily: _mi,
      ),
    ],
    totalBalanceMetric: SpendingMetric(
      label: 'Total balance',
      amountLabel: '\u00A314,826.46',
      trendLabel: '+4.6% vs last month',
      iconCodePoint: _iconStackedLineChart,
      iconFontFamily: _mi,
    ),
    netWorthMetric: SpendingMetric(
      label: 'Net worth',
      amountLabel: '\u00A322,180.64',
      trendLabel: '+\u00A3920 this month',
      iconCodePoint: _iconDiamondOutlined,
      iconFontFamily: _mi,
    ),
    safeToSpendLabel: '\u00A3820.00',
    safeToSpendSubtitle:
        'After bills, goals, and your weekly safety buffer.',
    breakdownSlices: <SpendingBreakdownSlice>[
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
    trendSpots: <SpendingTrendSpot>[
      SpendingTrendSpot(x: 0, y: 360),
      SpendingTrendSpot(x: 1, y: 410),
      SpendingTrendSpot(x: 2, y: 325),
      SpendingTrendSpot(x: 3, y: 298),
      SpendingTrendSpot(x: 4, y: 340),
    ],
    trendBottomLabels: <String>['W1', 'W2', 'W3', 'W4', 'Now'],
    insightTitle:
        'Your food spending is 12% higher than usual this week.',
    insightBody:
        'Most of the lift came from weekday deliveries after 8pm.',
    allocationSlices: <SpendingAllocationSlice>[
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
    recentTransactions: <SpendingRecentTransaction>[
      SpendingRecentTransaction(
        merchant: 'Uber',
        category: 'Transport',
        amountLabel: '\u00A314.20',
        iconText: 'U',
        iconBackgroundKey: 'dark',
        iconForegroundKey: 'surfaceBase',
      ),
      SpendingRecentTransaction(
        merchant: 'Amazon',
        category: 'Shopping',
        amountLabel: '\u00A327.99',
        iconText: 'a',
        iconBackgroundKey: 'warmSurface',
        iconForegroundKey: 'dark',
      ),
      SpendingRecentTransaction(
        merchant: "Nando's",
        category: 'Dining',
        amountLabel: '\u00A328.45',
        iconText: 'N',
        iconBackgroundKey: 'warmAccent',
        iconForegroundKey: 'warmText',
      ),
      SpendingRecentTransaction(
        merchant: 'Shoprite Lekki',
        category: 'Groceries',
        amountLabel: '\u20A618,500',
        iconText: 'SR',
        iconBackgroundKey: 'dark',
        iconForegroundKey: 'surfaceBase',
      ),
      SpendingRecentTransaction(
        merchant: 'Eko Electricity',
        category: 'Utilities',
        amountLabel: '\u20A612,000',
        iconText: 'EE',
        iconBackgroundKey: 'warmSurface',
        iconForegroundKey: 'dark',
      ),
    ],
  );

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

    if (demoDataMode == DemoDataMode.fresh) {
      return _freshOverview;
    }

    return _populatedOverview;
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
