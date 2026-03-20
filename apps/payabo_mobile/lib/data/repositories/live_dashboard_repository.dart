import 'dart:developer' as developer;

import 'package:dio/dio.dart';

import '../api/api_exception.dart';
import 'dashboard_repository.dart';

/// Live implementation of [DashboardRepository] that calls the
/// `GET /personal-finance/dashboard` BFF endpoint and maps the
/// server response into the existing [DashboardSummary] model.
class LiveDashboardRepository implements DashboardRepository {
  LiveDashboardRepository({required Dio apiClient}) : _apiClient = apiClient;

  final Dio _apiClient;

  @override
  Future<DashboardSummary> getSummary() async {
    try {
      final response = await _apiClient.get<Map<String, dynamic>>(
        '/personal-finance/dashboard',
      );

      final Map<String, dynamic> data = response.data ?? const {};
      return _mapSummary(data);
    } on DioException catch (exception) {
      _logDioFailure('getSummary', exception);
      throw mapDioException(exception);
    }
  }

  // ═══════════════════════════════════════════════════════════════
  // Mapping helpers
  // ═══════════════════════════════════════════════════════════════

  DashboardSummary _mapSummary(Map<String, dynamic> data) {
    final metricsJson =
        data['metrics'] as Map<String, dynamic>? ?? const {};
    final billsJson =
        data['upcomingBills'] as List<dynamic>? ?? const [];
    final ordersJson =
        data['recentOrders'] as List<dynamic>? ?? const [];
    final overviewJson =
        data['overview'] as Map<String, dynamic>? ?? const {};

    return DashboardSummary(
      metrics: _mapMetrics(metricsJson),
      upcomingBills: billsJson
          .whereType<Map<Object?, Object?>>()
          .map((b) => _mapBill(Map<String, dynamic>.from(b)))
          .toList(growable: false),
      recentTransactions: const <DashboardTransaction>[],
      recentOrders: ordersJson
          .whereType<Map<Object?, Object?>>()
          .map((o) => _mapOrder(Map<String, dynamic>.from(o)))
          .toList(growable: false),
      overviewSlices: _mapOverviewSlices(overviewJson),
      overviewMonthLabel: _readString(overviewJson['monthLabel']) ?? '',
      overviewMonthShortLabel:
          _readString(overviewJson['monthShortLabel']) ?? '',
      overviewYearLabel: _readString(overviewJson['yearLabel']) ?? '',
      todayInsight: const DashboardTodayInsight(
        message: '',
        actionLabel: '',
        timestampLabel: 'Today',
      ),
    );
  }

  DashboardMetrics _mapMetrics(Map<String, dynamic> json) {
    return DashboardMetrics(
      spendableLabel:
          _readString(json['availableToSpendLabel']) ?? '£0.00',
      spendableSubtitle:
          _readString(json['availableToSpendSubtitle']) ?? '',
      spendableProgress:
          (json['spendableProgress'] as num?)?.toDouble() ?? 0.0,
      spendableProgressLabel:
          _readString(json['spendableProgressLabel']) ?? '0% free',
      netWorthLabel:
          _readString(json['netWorthLabel']) ?? '£0.00',
      netWorthChangeLabel:
          _readString(json['netWorthChangeLabel']) ?? '+£0.00',
      netWorthTrendLabel:
          _readString(json['netWorthTrendLabel']) ?? 'up 0%',
      assetsLabel:
          _readString(json['assetsLabel']) ?? '£0',
      billsLabel:
          _readString(json['billsLabel']) ?? '£0',
    );
  }

  DashboardUpcomingBill _mapBill(Map<String, dynamic> json) {
    return DashboardUpcomingBill(
      id: _readString(json['id']) ?? '',
      biller: _readString(json['payee']) ?? 'Unknown',
      amountLabel: _readString(json['amountLabel']) ?? '',
      dueDateLabel: _readString(json['dueDateLabel']) ?? '',
    );
  }

  DashboardRecentOrder _mapOrder(Map<String, dynamic> json) {
    return DashboardRecentOrder(
      id: _readString(json['id']) ?? '',
      beneficiaryName:
          _readString(json['beneficiaryName']) ?? 'Unknown',
      amountLabel: _readString(json['amountLabel']) ?? '',
      orderType: _readString(json['orderType']) ?? '',
      dateLabel: _readString(json['dateLabel']) ?? '',
      status: _readString(json['status']) ?? '',
      beneficiaryPhotoUrl: _readString(json['beneficiaryPhotoUrl']),
    );
  }

  List<DashboardOverviewSlice> _mapOverviewSlices(
    Map<String, dynamic> overviewJson,
  ) {
    final slicesJson =
        overviewJson['slices'] as List<dynamic>? ?? const [];

    return slicesJson
        .whereType<Map<Object?, Object?>>()
        .map((s) {
          final slice = Map<String, dynamic>.from(s);
          return DashboardOverviewSlice(
            label: _readString(slice['label']) ?? '',
            amountLabel: _readString(slice['amountLabel']) ?? '',
            value: (slice['amount'] as num?)?.toDouble() ?? 0.0,
            colorKey: _readString(slice['colorKey']) ?? 'primary',
          );
        })
        .toList(growable: false);
  }

  // ═══════════════════════════════════════════════════════════════
  // Utilities
  // ═══════════════════════════════════════════════════════════════

  String? _readString(Object? value) {
    if (value is! String) return null;
    final trimmed = value.trim();
    return trimmed.isEmpty ? null : trimmed;
  }

  void _logDioFailure(String operation, DioException exception) {
    final request = exception.requestOptions;
    final statusCode = exception.response?.statusCode;

    developer.log(
      '$operation failed for ${request.method} ${request.path}'
      '${statusCode != null ? ' (HTTP $statusCode)' : ''} '
      '[${exception.type.name}].',
      name: 'Payabo.LiveDashboardRepository',
      stackTrace: exception.stackTrace,
    );
  }
}
