import 'dart:developer' as developer;

import 'package:dio/dio.dart';
import 'package:intl/intl.dart';

import '../api/api_exception.dart';
import 'commitments_repository.dart';

class LiveCommitmentsRepository implements CommitmentsRepository {
  LiveCommitmentsRepository({required Dio apiClient})
      : _apiClient = apiClient;

  final Dio _apiClient;

  static final _currencyFormatter =
      NumberFormat.currency(symbol: '£', decimalDigits: 2);

  @override
  Future<CommitmentListPage> listCommitments() async {
    try {
      final response = await _apiClient.get<Map<String, dynamic>>(
        '/personal-finance/commitments',
        queryParameters: <String, dynamic>{
          'status': 'Active',
          'pageSize': 100,
        },
      );

      final data = response.data ?? const <String, dynamic>{};
      return _mapPage(data);
    } on DioException catch (e) {
      _log('listCommitments', e);
      throw mapDioException(e);
    }
  }

  @override
  Future<void> confirmCommitment(String id) async {
    try {
      await _apiClient.post<void>(
        '/personal-finance/commitments/$id/confirm',
      );
    } on DioException catch (e) {
      _log('confirmCommitment', e);
      throw mapDioException(e);
    }
  }

  @override
  Future<void> rejectCommitment(String id) async {
    try {
      await _apiClient.post<void>(
        '/personal-finance/commitments/$id/reject',
      );
    } on DioException catch (e) {
      _log('rejectCommitment', e);
      throw mapDioException(e);
    }
  }

  // ═══════════════════════════════════════════════════════
  // Mapping
  // ═══════════════════════════════════════════════════════

  CommitmentListPage _mapPage(Map<String, dynamic> data) {
    final itemsJson = data['items'] as List<dynamic>? ?? const [];
    final totalsJson =
        data['totals'] as Map<String, dynamic>? ?? const {};

    final items = itemsJson
        .whereType<Map<Object?, Object?>>()
        .map((j) => _mapItem(Map<String, dynamic>.from(j)))
        .toList(growable: false);

    return CommitmentListPage(
      items: items,
      totals: _mapTotals(totalsJson),
    );
  }

  CommitmentItem _mapItem(Map<String, dynamic> j) {
    final rawType = _str(j['commitmentType']) ?? '';
    final rawStatus = _str(j['verificationStatus']) ?? '';
    final rawDue = _str(j['dueDate']);
    final amount = (j['amount'] as num?)?.toDouble();

    return CommitmentItem(
      id: _str(j['commitmentId']) ?? '',
      type: _parseType(rawType),
      verificationStatus: _parseVerification(rawStatus),
      displayName: _str(j['displayName']) ?? '',
      amount: amount,
      amountLabel: amount != null
          ? _currencyFormatter.format(amount)
          : _str(j['amountLabel']),
      currency: _str(j['currency']),
      dueDate: rawDue != null ? DateTime.tryParse(rawDue) : null,
      dueDateLabel: _str(j['dueDateLabel']),
      frequency: _str(j['frequency']),
      autopay: j['autopay'] as bool? ?? false,
      category: _str(j['category']),
      confidenceScore: (j['confidenceScore'] as num?)?.toDouble(),
    );
  }

  CommitmentTotals _mapTotals(Map<String, dynamic> j) {
    final total = (j['totalUpcomingAmount'] as num?)?.toDouble() ?? 0;
    return CommitmentTotals(
      totalUpcomingAmountLabel: _currencyFormatter.format(total),
      dueSoonCount: j['dueSoonCount'] as int? ?? 0,
      detectedCount: j['detectedCount'] as int? ?? 0,
      billsCount: j['billsCount'] as int? ?? 0,
      subscriptionsCount: j['subscriptionsCount'] as int? ?? 0,
      debtRepaymentsCount: j['debtRepaymentsCount'] as int? ?? 0,
    );
  }

  static CommitmentType _parseType(String raw) {
    switch (raw.toLowerCase()) {
      case 'subscription':
        return CommitmentType.subscription;
      case 'debtrepayment':
        return CommitmentType.debtRepayment;
      case 'bill':
      default:
        return CommitmentType.bill;
    }
  }

  static CommitmentVerificationStatus _parseVerification(String raw) {
    switch (raw.toLowerCase()) {
      case 'confirmed':
        return CommitmentVerificationStatus.confirmed;
      case 'manual':
        return CommitmentVerificationStatus.manual;
      case 'imported':
        return CommitmentVerificationStatus.imported;
      case 'rejected':
        return CommitmentVerificationStatus.rejected;
      case 'archived':
        return CommitmentVerificationStatus.archived;
      case 'detected':
      default:
        return CommitmentVerificationStatus.detected;
    }
  }

  static String? _str(dynamic v) => v is String ? v : null;

  static void _log(String method, DioException e) {
    developer.log(
      'LiveCommitmentsRepository.$method failed: ${e.message}',
      name: 'CommitmentsRepository',
      error: e,
    );
  }
}
