// Repository abstraction for personal transactions.
//
// The backend exposes:
//   GET /personal-finance/transactions
//   Query params: personalAccountId, from, to, category, search, page, pageSize
//
// This file contains:
//   - PersonalTransactionItem      — display-ready view model
//   - PersonalTransactionsQuery    — query parameters
//   - PersonalTransactionsPage     — paginated result wrapper
//   - PersonalTransactionsRepository — abstract interface

class PersonalTransactionItem {
  const PersonalTransactionItem({
    required this.id,
    required this.merchant,
    required this.category,
    this.subCategory,
    required this.amount,
    required this.currency,
    required this.isCredit,
    required this.occurredAt,
    this.description,
    this.personalAccountId,
    this.sourceType,
  });

  /// Backend UUID (`personalTransactionId`).
  final String id;

  /// Merchant name, or description if no merchant.
  final String merchant;

  /// Category string from the backend (may be empty).
  final String category;

  /// System-assigned subcategory code (e.g. `'supermarket'`, `'streaming'`).
  /// Display-only — users cannot select subcategories directly.
  final String? subCategory;

  /// Signed decimal amount.  Negative = debit, positive = credit.
  final Decimal amount;

  /// ISO-4217 currency code, e.g. `'GBP'`.
  final String currency;

  /// True when money moved INTO the account (positive amount).
  final bool isCredit;

  final DateTime occurredAt;
  final String? description;
  final String? personalAccountId;

  /// Source type: `'manual'` for user-created, or other values for imported.
  final String? sourceType;

  /// Whether this transaction was manually created and can be deleted.
  bool get isManual =>
      sourceType != null && sourceType!.toLowerCase() == 'manual';

  // ── Pre-formatted display helpers ─────────────────────────────────────────

  /// Absolute value as a string with two decimal places, e.g. `'27.99'`.
  String get absoluteAmountLabel {
    final double abs = amount.abs().toDouble();
    return abs.toStringAsFixed(2);
  }

  /// Major part of the amount (before the decimal point), e.g. `'27'`.
  String get amountMajor {
    final double abs = amount.abs().toDouble();
    final int major = abs.truncate();
    return major.toString().replaceAllMapped(
          RegExp(r'(\d{1,3})(?=(\d{3})+(?!\d))'),
          (Match m) => '${m[1]},',
        );
  }

  /// Minor part including the decimal point, e.g. `'.99'`.
  String get amountMinor {
    final double abs = amount.abs().toDouble();
    final int major = abs.truncate();
    final int minorCents = ((abs - major) * 100).round();
    return '.${minorCents.toString().padLeft(2, '0')}';
  }

  /// Currency symbol derived from [currency].
  String get currencySymbol => _currencySymbol(currency);

  static String _currencySymbol(String code) {
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
      default:
        return code;
    }
  }
}

/// Supported sort orders for transaction listing.
enum PersonalTransactionsSortOrder { newestFirst, oldestFirst }

class PersonalTransactionsQuery {
  const PersonalTransactionsQuery({
    this.personalAccountId,
    this.from,
    this.to,
    this.category,
    this.search,
    this.page = 1,
    this.pageSize = 50,
  });

  final String? personalAccountId;
  final DateTime? from;
  final DateTime? to;
  final String? category;
  final String? search;
  final int page;
  final int pageSize;

  PersonalTransactionsQuery copyWith({
    Object? personalAccountId = _sentinel,
    DateTime? from,
    DateTime? to,
    Object? category = _sentinel,
    Object? search = _sentinel,
    int? page,
    int? pageSize,
  }) {
    return PersonalTransactionsQuery(
      personalAccountId: personalAccountId == _sentinel
          ? this.personalAccountId
          : personalAccountId as String?,
      from: from ?? this.from,
      to: to ?? this.to,
      category:
          category == _sentinel ? this.category : category as String?,
      search: search == _sentinel ? this.search : search as String?,
      page: page ?? this.page,
      pageSize: pageSize ?? this.pageSize,
    );
  }
}

const Object _sentinel = Object();

typedef Decimal = double;

class PersonalTransactionsPage {
  const PersonalTransactionsPage({
    required this.items,
    required this.page,
    required this.pageSize,
    required this.hasMore,
  });

  final List<PersonalTransactionItem> items;
  final int page;
  final int pageSize;

  /// True when there may be a next page (i.e. items.length == pageSize).
  final bool hasMore;

  PersonalTransactionsPage get empty => const PersonalTransactionsPage(
        items: <PersonalTransactionItem>[],
        page: 1,
        pageSize: 50,
        hasMore: false,
      );
}

abstract class PersonalTransactionsRepository {
  Future<PersonalTransactionsPage> listTransactions(
    PersonalTransactionsQuery query,
  );

  Future<PersonalTransactionItem?> getTransaction(String transactionId);

  /// Deletes a manually created transaction. Throws if the transaction
  /// is not manual or does not exist.
  Future<void> deleteTransaction(String transactionId);
}
