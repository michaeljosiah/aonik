import '../../app/demo/demo_data_mode.dart';
import '../../data/repositories/personal_transactions_repository.dart';
import '../mock_behavior.dart';

class MockPersonalTransactionsRepository
    implements PersonalTransactionsRepository {
  MockPersonalTransactionsRepository({
    this.demoDataMode = DemoDataMode.populated,
  }) : _transactions = demoDataMode == DemoDataMode.fresh
            ? <PersonalTransactionItem>[]
            : List<PersonalTransactionItem>.of(_seedTransactions);

  final DemoDataMode demoDataMode;

  final List<PersonalTransactionItem> _transactions;

  // ─────────────────────────────────────────────────────────
  //  Seed data (populated mode)
  //
  //  Comprehensive set of transactions across Ghana (GHS),
  //  UK (GBP), and Nigeria (NGN) accounts.
  // ─────────────────────────────────────────────────────────

  static final List<PersonalTransactionItem> _seedTransactions =
      <PersonalTransactionItem>[
    // ── Ghana (GHS) — Current account ──────────────────────
    PersonalTransactionItem(
      id: 'ptx-001',
      merchant: 'Shoprite',
      category: 'Groceries',
      amount: -87.50,
      currency: 'GHS',
      isCredit: false,
      occurredAt: DateTime(2026, 3, 17, 9, 14),
      description: 'Weekly groceries',
      personalAccountId: 'acc-gh-current',
    ),
    PersonalTransactionItem(
      id: 'ptx-002',
      merchant: 'MTN Mobile Money',
      category: 'Transfer',
      amount: -300.00,
      currency: 'GHS',
      isCredit: false,
      occurredAt: DateTime(2026, 3, 16, 20, 3),
      description: 'Transfer to Ama Boafo',
      personalAccountId: 'acc-gh-current',
    ),
    PersonalTransactionItem(
      id: 'ptx-003',
      merchant: 'Employer Payroll',
      category: 'Income',
      amount: 4232.24,
      currency: 'GHS',
      isCredit: true,
      occurredAt: DateTime(2026, 3, 15, 8, 0),
      description: 'March salary',
      personalAccountId: 'acc-gh-current',
    ),
    PersonalTransactionItem(
      id: 'ptx-004',
      merchant: 'ECG Power',
      category: 'Utilities',
      amount: -150.00,
      currency: 'GHS',
      isCredit: false,
      occurredAt: DateTime(2026, 3, 14, 14, 22),
      description: 'Electricity prepaid',
      personalAccountId: 'acc-gh-current',
    ),
    PersonalTransactionItem(
      id: 'ptx-005',
      merchant: 'Uber',
      category: 'Transport',
      amount: -32.80,
      currency: 'GHS',
      isCredit: false,
      occurredAt: DateTime(2026, 3, 14, 7, 45),
      description: 'Ride to office',
      personalAccountId: 'acc-gh-current',
    ),
    PersonalTransactionItem(
      id: 'ptx-006',
      merchant: 'Netflix',
      category: 'Entertainment',
      amount: -58.00,
      currency: 'GHS',
      isCredit: false,
      occurredAt: DateTime(2026, 3, 13, 0, 5),
      description: 'Monthly subscription',
      personalAccountId: 'acc-gh-current',
    ),
    PersonalTransactionItem(
      id: 'ptx-007',
      merchant: 'KFC Accra Mall',
      category: 'Dining',
      amount: -45.00,
      currency: 'GHS',
      isCredit: false,
      occurredAt: DateTime(2026, 3, 12, 13, 10),
      description: 'Lunch',
      personalAccountId: 'acc-gh-current',
    ),
    PersonalTransactionItem(
      id: 'ptx-008',
      merchant: 'Ghana Water',
      category: 'Utilities',
      amount: -90.00,
      currency: 'GHS',
      isCredit: false,
      occurredAt: DateTime(2026, 3, 11, 10, 30),
      description: 'Water bill',
      personalAccountId: 'acc-gh-current',
    ),
    PersonalTransactionItem(
      id: 'ptx-009',
      merchant: 'Melcom',
      category: 'Shopping',
      amount: -125.55,
      currency: 'GHS',
      isCredit: false,
      occurredAt: DateTime(2026, 3, 10, 16, 40),
      description: 'Personal care items',
      personalAccountId: 'acc-gh-current',
    ),
    PersonalTransactionItem(
      id: 'ptx-010',
      merchant: 'Bolt',
      category: 'Transport',
      amount: -18.50,
      currency: 'GHS',
      isCredit: false,
      occurredAt: DateTime(2026, 3, 10, 8, 15),
      description: 'Morning ride',
      personalAccountId: 'acc-gh-current',
    ),
    PersonalTransactionItem(
      id: 'ptx-011',
      merchant: 'Stanbic Bank Interest',
      category: 'Income',
      amount: 12.40,
      currency: 'GHS',
      isCredit: true,
      occurredAt: DateTime(2026, 3, 9, 0, 0),
      description: 'Savings interest',
      personalAccountId: 'acc-gh-savings',
    ),
    PersonalTransactionItem(
      id: 'ptx-012',
      merchant: 'DSTV',
      category: 'Entertainment',
      amount: -220.00,
      currency: 'GHS',
      isCredit: false,
      occurredAt: DateTime(2026, 3, 8, 9, 0),
      description: 'Premium subscription',
      personalAccountId: 'acc-gh-current',
    ),
    PersonalTransactionItem(
      id: 'ptx-013',
      merchant: 'Shell Fuel Station',
      category: 'Transport',
      amount: -232.20,
      currency: 'GHS',
      isCredit: false,
      occurredAt: DateTime(2026, 3, 7, 17, 30),
      description: 'Fuel top-up',
      personalAccountId: 'acc-gh-current',
    ),
    PersonalTransactionItem(
      id: 'ptx-014',
      merchant: 'Market Circle',
      category: 'Groceries',
      amount: -123.65,
      currency: 'GHS',
      isCredit: false,
      occurredAt: DateTime(2026, 3, 6, 11, 0),
      description: 'Fresh market produce',
      personalAccountId: 'acc-gh-current',
    ),
    PersonalTransactionItem(
      id: 'ptx-015',
      merchant: 'MTN Fibre',
      category: 'Utilities',
      amount: -180.00,
      currency: 'GHS',
      isCredit: false,
      occurredAt: DateTime(2026, 3, 5, 10, 0),
      description: 'Internet subscription',
      personalAccountId: 'acc-gh-current',
    ),
    PersonalTransactionItem(
      id: 'ptx-016',
      merchant: 'Mama Grace',
      category: 'Family Support',
      amount: -500.00,
      currency: 'GHS',
      isCredit: false,
      occurredAt: DateTime(2026, 3, 4, 9, 0),
      description: 'Monthly living expenses support',
      personalAccountId: 'acc-gh-current',
    ),
    PersonalTransactionItem(
      id: 'ptx-017',
      merchant: 'Vida e Caffe',
      category: 'Dining',
      amount: -22.00,
      currency: 'GHS',
      isCredit: false,
      occurredAt: DateTime(2026, 3, 3, 8, 20),
      description: 'Morning coffee',
      personalAccountId: 'acc-gh-current',
    ),
    PersonalTransactionItem(
      id: 'ptx-018',
      merchant: 'Freelance Client',
      category: 'Income',
      amount: 800.00,
      currency: 'GHS',
      isCredit: true,
      occurredAt: DateTime(2026, 3, 2, 15, 0),
      description: 'Design project payment',
      personalAccountId: 'acc-gh-current',
    ),
    PersonalTransactionItem(
      id: 'ptx-019',
      merchant: 'Pharmacy One',
      category: 'Health',
      amount: -52.00,
      currency: 'GHS',
      isCredit: false,
      occurredAt: DateTime(2026, 3, 1, 12, 30),
      description: 'Prescription refill',
      personalAccountId: 'acc-gh-current',
    ),
    PersonalTransactionItem(
      id: 'ptx-020',
      merchant: 'Gym Plus',
      category: 'Health',
      amount: -80.00,
      currency: 'GHS',
      isCredit: false,
      occurredAt: DateTime(2026, 3, 1, 7, 0),
      description: 'Monthly gym membership',
      personalAccountId: 'acc-gh-current',
    ),
    PersonalTransactionItem(
      id: 'ptx-021',
      merchant: 'Papaye',
      category: 'Dining',
      amount: -38.00,
      currency: 'GHS',
      isCredit: false,
      occurredAt: DateTime(2026, 2, 28, 13, 45),
      description: 'Lunch with colleagues',
      personalAccountId: 'acc-gh-current',
    ),
    PersonalTransactionItem(
      id: 'ptx-022',
      merchant: 'Vodafone',
      category: 'Utilities',
      amount: -35.00,
      currency: 'GHS',
      isCredit: false,
      occurredAt: DateTime(2026, 2, 27, 10, 0),
      description: 'Data bundle',
      personalAccountId: 'acc-gh-current',
    ),

    // ── UK (GBP) — UK Current account ──────────────────────
    PersonalTransactionItem(
      id: 'ptx-uk-001',
      merchant: 'Tesco',
      category: 'Groceries',
      amount: -54.12,
      currency: 'GBP',
      isCredit: false,
      occurredAt: DateTime(2026, 3, 16, 18, 30),
      description: 'Weekly shop',
      personalAccountId: 'acc-uk-current',
    ),
    PersonalTransactionItem(
      id: 'ptx-uk-002',
      merchant: 'Open Rent',
      category: 'Housing',
      amount: 1450.00,
      currency: 'GBP',
      isCredit: true,
      occurredAt: DateTime(2026, 3, 15, 0, 0),
      description: 'Monthly rent received from tenant',
      personalAccountId: 'acc-uk-current',
    ),
    PersonalTransactionItem(
      id: 'ptx-uk-003',
      merchant: 'Uber',
      category: 'Transport',
      amount: -14.20,
      currency: 'GBP',
      isCredit: false,
      occurredAt: DateTime(2026, 3, 14, 22, 15),
      description: 'Evening ride home',
      personalAccountId: 'acc-uk-current',
    ),
    PersonalTransactionItem(
      id: 'ptx-uk-004',
      merchant: 'Amazon',
      category: 'Shopping',
      amount: -27.99,
      currency: 'GBP',
      isCredit: false,
      occurredAt: DateTime(2026, 3, 14, 10, 0),
      description: 'Household items',
      personalAccountId: 'acc-uk-current',
    ),
    PersonalTransactionItem(
      id: 'ptx-uk-005',
      merchant: "Nando's",
      category: 'Dining',
      amount: -28.45,
      currency: 'GBP',
      isCredit: false,
      occurredAt: DateTime(2026, 3, 13, 19, 30),
      description: 'Dinner with friends',
      personalAccountId: 'acc-uk-current',
    ),
    PersonalTransactionItem(
      id: 'ptx-uk-006',
      merchant: 'TfL',
      category: 'Transport',
      amount: -7.40,
      currency: 'GBP',
      isCredit: false,
      occurredAt: DateTime(2026, 3, 13, 8, 0),
      description: 'Tube fare',
      personalAccountId: 'acc-uk-current',
    ),
    PersonalTransactionItem(
      id: 'ptx-uk-007',
      merchant: 'Sainsbury\'s',
      category: 'Groceries',
      amount: -62.30,
      currency: 'GBP',
      isCredit: false,
      occurredAt: DateTime(2026, 3, 12, 17, 0),
      description: 'Groceries and essentials',
      personalAccountId: 'acc-uk-current',
    ),
    PersonalTransactionItem(
      id: 'ptx-uk-008',
      merchant: 'Shell',
      category: 'Transport',
      amount: -58.40,
      currency: 'GBP',
      isCredit: false,
      occurredAt: DateTime(2026, 3, 11, 7, 30),
      description: 'Fuel',
      personalAccountId: 'acc-uk-current',
    ),
    PersonalTransactionItem(
      id: 'ptx-uk-009',
      merchant: 'Gym Group',
      category: 'Health',
      amount: -24.99,
      currency: 'GBP',
      isCredit: false,
      occurredAt: DateTime(2026, 3, 10, 6, 0),
      description: 'Monthly gym membership',
      personalAccountId: 'acc-uk-current',
    ),
    PersonalTransactionItem(
      id: 'ptx-uk-010',
      merchant: 'British Gas',
      category: 'Utilities',
      amount: -86.00,
      currency: 'GBP',
      isCredit: false,
      occurredAt: DateTime(2026, 3, 7, 0, 0),
      description: 'Gas and electricity',
      personalAccountId: 'acc-uk-current',
    ),
    PersonalTransactionItem(
      id: 'ptx-uk-011',
      merchant: 'Vodafone UK',
      category: 'Utilities',
      amount: -25.00,
      currency: 'GBP',
      isCredit: false,
      occurredAt: DateTime(2026, 3, 5, 0, 0),
      description: 'Mobile contract',
      personalAccountId: 'acc-uk-current',
    ),
    PersonalTransactionItem(
      id: 'ptx-uk-012',
      merchant: 'John Lewis',
      category: 'Shopping',
      amount: -89.00,
      currency: 'GBP',
      isCredit: false,
      occurredAt: DateTime(2026, 3, 4, 14, 0),
      description: 'Cookware set',
      personalAccountId: 'acc-uk-current',
    ),
    PersonalTransactionItem(
      id: 'ptx-uk-013',
      merchant: 'Deliveroo',
      category: 'Dining',
      amount: -19.80,
      currency: 'GBP',
      isCredit: false,
      occurredAt: DateTime(2026, 3, 3, 20, 45),
      description: 'Late night delivery',
      personalAccountId: 'acc-uk-current',
    ),
    PersonalTransactionItem(
      id: 'ptx-uk-014',
      merchant: 'Pret A Manger',
      category: 'Dining',
      amount: -5.60,
      currency: 'GBP',
      isCredit: false,
      occurredAt: DateTime(2026, 3, 3, 8, 15),
      description: 'Morning coffee and pastry',
      personalAccountId: 'acc-uk-current',
    ),
    PersonalTransactionItem(
      id: 'ptx-uk-015',
      merchant: 'Starling Interest',
      category: 'Income',
      amount: 4.80,
      currency: 'GBP',
      isCredit: true,
      occurredAt: DateTime(2026, 3, 1, 0, 0),
      description: 'Savings interest',
      personalAccountId: 'acc-uk-savings',
    ),
    PersonalTransactionItem(
      id: 'ptx-uk-016',
      merchant: 'Auto-save',
      category: 'Savings',
      amount: 200.00,
      currency: 'GBP',
      isCredit: true,
      occurredAt: DateTime(2026, 3, 1, 0, 0),
      description: 'Monthly auto-save to UK Savings',
      personalAccountId: 'acc-uk-savings',
    ),

    // ── UK Credit Card (GBP) ──────────────────────────────
    PersonalTransactionItem(
      id: 'ptx-uk-cc-001',
      merchant: 'Netflix',
      category: 'Entertainment',
      amount: -15.99,
      currency: 'GBP',
      isCredit: false,
      occurredAt: DateTime(2026, 3, 15, 0, 0),
      description: 'Monthly subscription',
      personalAccountId: 'acc-uk-credit',
    ),
    PersonalTransactionItem(
      id: 'ptx-uk-cc-002',
      merchant: 'Spotify',
      category: 'Entertainment',
      amount: -10.99,
      currency: 'GBP',
      isCredit: false,
      occurredAt: DateTime(2026, 3, 14, 0, 0),
      description: 'Premium subscription',
      personalAccountId: 'acc-uk-credit',
    ),
    PersonalTransactionItem(
      id: 'ptx-uk-cc-003',
      merchant: 'ASOS',
      category: 'Shopping',
      amount: -45.60,
      currency: 'GBP',
      isCredit: false,
      occurredAt: DateTime(2026, 3, 8, 11, 0),
      description: 'Clothing order',
      personalAccountId: 'acc-uk-credit',
    ),
    PersonalTransactionItem(
      id: 'ptx-uk-cc-004',
      merchant: 'EasyJet',
      category: 'Travel',
      amount: -189.00,
      currency: 'GBP',
      isCredit: false,
      occurredAt: DateTime(2026, 3, 5, 16, 0),
      description: 'Flight to Accra (April)',
      personalAccountId: 'acc-uk-credit',
    ),

    // ── Nigeria (NGN) — Naira Current ─────────────────────
    PersonalTransactionItem(
      id: 'ptx-ng-001',
      merchant: 'Shoprite Lekki',
      category: 'Groceries',
      amount: -18500.00,
      currency: 'NGN',
      isCredit: false,
      occurredAt: DateTime(2026, 3, 17, 11, 0),
      description: 'Weekly groceries',
      personalAccountId: 'acc-ng-current',
    ),
    PersonalTransactionItem(
      id: 'ptx-ng-002',
      merchant: 'Eko Electricity',
      category: 'Utilities',
      amount: -12000.00,
      currency: 'NGN',
      isCredit: false,
      occurredAt: DateTime(2026, 3, 16, 9, 0),
      description: 'Prepaid meter top-up',
      personalAccountId: 'acc-ng-current',
    ),
    PersonalTransactionItem(
      id: 'ptx-ng-003',
      merchant: 'Bolt',
      category: 'Transport',
      amount: -4200.00,
      currency: 'NGN',
      isCredit: false,
      occurredAt: DateTime(2026, 3, 15, 8, 30),
      description: 'Ride to Victoria Island',
      personalAccountId: 'acc-ng-current',
    ),
    PersonalTransactionItem(
      id: 'ptx-ng-004',
      merchant: 'MTN Data',
      category: 'Utilities',
      amount: -3500.00,
      currency: 'NGN',
      isCredit: false,
      occurredAt: DateTime(2026, 3, 14, 10, 0),
      description: '10GB data bundle',
      personalAccountId: 'acc-ng-current',
    ),
    PersonalTransactionItem(
      id: 'ptx-ng-005',
      merchant: 'Chicken Republic',
      category: 'Dining',
      amount: -5800.00,
      currency: 'NGN',
      isCredit: false,
      occurredAt: DateTime(2026, 3, 13, 13, 30),
      description: 'Lunch',
      personalAccountId: 'acc-ng-current',
    ),
    PersonalTransactionItem(
      id: 'ptx-ng-006',
      merchant: 'Salary Credit',
      category: 'Income',
      amount: 750000.00,
      currency: 'NGN',
      isCredit: true,
      occurredAt: DateTime(2026, 3, 12, 8, 0),
      description: 'March salary from Lagos office',
      personalAccountId: 'acc-ng-current',
    ),
    PersonalTransactionItem(
      id: 'ptx-ng-007',
      merchant: 'Lagos Water Corp',
      category: 'Utilities',
      amount: -8000.00,
      currency: 'NGN',
      isCredit: false,
      occurredAt: DateTime(2026, 3, 11, 9, 0),
      description: 'Water bill',
      personalAccountId: 'acc-ng-current',
    ),
    PersonalTransactionItem(
      id: 'ptx-ng-008',
      merchant: 'Jumia',
      category: 'Shopping',
      amount: -25400.00,
      currency: 'NGN',
      isCredit: false,
      occurredAt: DateTime(2026, 3, 10, 15, 0),
      description: 'Electronics purchase',
      personalAccountId: 'acc-ng-current',
    ),
    PersonalTransactionItem(
      id: 'ptx-ng-009',
      merchant: 'DStv',
      category: 'Entertainment',
      amount: -21000.00,
      currency: 'NGN',
      isCredit: false,
      occurredAt: DateTime(2026, 3, 9, 0, 0),
      description: 'Premium subscription',
      personalAccountId: 'acc-ng-current',
    ),
    PersonalTransactionItem(
      id: 'ptx-ng-010',
      merchant: 'Mama Put',
      category: 'Dining',
      amount: -2500.00,
      currency: 'NGN',
      isCredit: false,
      occurredAt: DateTime(2026, 3, 8, 12, 30),
      description: 'Local food',
      personalAccountId: 'acc-ng-current',
    ),
    PersonalTransactionItem(
      id: 'ptx-ng-011',
      merchant: 'Uber',
      category: 'Transport',
      amount: -3800.00,
      currency: 'NGN',
      isCredit: false,
      occurredAt: DateTime(2026, 3, 7, 19, 0),
      description: 'Evening ride',
      personalAccountId: 'acc-ng-current',
    ),
    PersonalTransactionItem(
      id: 'ptx-ng-012',
      merchant: 'Total Fuel',
      category: 'Transport',
      amount: -15000.00,
      currency: 'NGN',
      isCredit: false,
      occurredAt: DateTime(2026, 3, 6, 7, 0),
      description: 'Fuel top-up',
      personalAccountId: 'acc-ng-current',
    ),
    PersonalTransactionItem(
      id: 'ptx-ng-013',
      merchant: 'Auto-save',
      category: 'Savings',
      amount: 50000.00,
      currency: 'NGN',
      isCredit: true,
      occurredAt: DateTime(2026, 3, 5, 0, 0),
      description: 'Monthly auto-save to Kuda Savings',
      personalAccountId: 'acc-ng-savings',
    ),
    PersonalTransactionItem(
      id: 'ptx-ng-014',
      merchant: 'Kuda Interest',
      category: 'Income',
      amount: 1250.00,
      currency: 'NGN',
      isCredit: true,
      occurredAt: DateTime(2026, 3, 1, 0, 0),
      description: 'Savings interest',
      personalAccountId: 'acc-ng-savings',
    ),

    // ── Nigeria (USD) — Domiciliary ───────────────────────
    PersonalTransactionItem(
      id: 'ptx-ng-usd-001',
      merchant: 'Freelance Client (USD)',
      category: 'Income',
      amount: 1200.00,
      currency: 'USD',
      isCredit: true,
      occurredAt: DateTime(2026, 3, 10, 14, 0),
      description: 'Contract payment from US client',
      personalAccountId: 'acc-ng-dom',
    ),
    PersonalTransactionItem(
      id: 'ptx-ng-usd-002',
      merchant: 'FX Conversion to NGN',
      category: 'Transfer',
      amount: -500.00,
      currency: 'USD',
      isCredit: false,
      occurredAt: DateTime(2026, 3, 8, 11, 0),
      description: 'Converted to Naira at 1,580/\$',
      personalAccountId: 'acc-ng-dom',
    ),
  ];

  // ─────────────────────────────────────────────────────────
  //  Repository implementation
  // ─────────────────────────────────────────────────────────

  @override
  Future<PersonalTransactionsPage> listTransactions(
    PersonalTransactionsQuery query,
  ) async {
    await MockBehavior.delay();
    MockBehavior.throwIfEnabled('personalTransactions.listTransactions');

    List<PersonalTransactionItem> filtered =
        List<PersonalTransactionItem>.of(_transactions);

    // Filter by account
    if (query.personalAccountId != null) {
      filtered = filtered
          .where((PersonalTransactionItem t) =>
              t.personalAccountId == query.personalAccountId)
          .toList();
    }

    // Filter by date range
    if (query.from != null) {
      filtered = filtered
          .where((PersonalTransactionItem t) =>
              !t.occurredAt.isBefore(query.from!))
          .toList();
    }
    if (query.to != null) {
      filtered = filtered
          .where((PersonalTransactionItem t) =>
              !t.occurredAt.isAfter(query.to!))
          .toList();
    }

    // Filter by category
    if (query.category != null && query.category!.isNotEmpty) {
      final String lowerCategory = query.category!.toLowerCase();
      filtered = filtered
          .where((PersonalTransactionItem t) =>
              t.category.toLowerCase() == lowerCategory)
          .toList();
    }

    // Filter by search term
    if (query.search != null && query.search!.isNotEmpty) {
      final String lowerSearch = query.search!.toLowerCase();
      filtered = filtered
          .where((PersonalTransactionItem t) =>
              t.merchant.toLowerCase().contains(lowerSearch) ||
              (t.description?.toLowerCase().contains(lowerSearch) ?? false) ||
              t.category.toLowerCase().contains(lowerSearch))
          .toList();
    }

    // Sort newest first
    filtered.sort((PersonalTransactionItem a, PersonalTransactionItem b) =>
        b.occurredAt.compareTo(a.occurredAt));

    // Paginate
    final int startIndex = (query.page - 1) * query.pageSize;
    if (startIndex >= filtered.length) {
      return const PersonalTransactionsPage(
        items: <PersonalTransactionItem>[],
        page: 1,
        pageSize: 50,
        hasMore: false,
      );
    }

    final int endIndex = (startIndex + query.pageSize).clamp(0, filtered.length);
    final List<PersonalTransactionItem> pageItems =
        filtered.sublist(startIndex, endIndex);

    return PersonalTransactionsPage(
      items: pageItems,
      page: query.page,
      pageSize: query.pageSize,
      hasMore: endIndex < filtered.length,
    );
  }

  @override
  Future<PersonalTransactionItem?> getTransaction(
    String transactionId,
  ) async {
    await MockBehavior.delay();
    MockBehavior.throwIfEnabled('personalTransactions.getTransaction');

    for (final PersonalTransactionItem item in _transactions) {
      if (item.id == transactionId) {
        return item;
      }
    }

    return null;
  }
}
