import 'package:dio/dio.dart';

import '../../features/setup_journey/domain/setup_enums.dart';
import '../../features/setup_journey/domain/setup_journey_repository.dart';
import '../../features/setup_journey/domain/setup_models.dart';
import '../api/api_exception.dart';

class LiveSetupJourneyRepository implements SetupJourneyRepository {
  LiveSetupJourneyRepository({required Dio apiClient}) : _apiClient = apiClient;

  final Dio _apiClient;

  @override
  Future<void> saveSetupProfile(PayaboSetupProfile profile) async {
    try {
      await _apiClient.put<void>(
        '/personal-finance/setup-profile',
        data: <String, dynamic>{
          'selectedUseCases': profile.selectedUseCases
              .map((SetupUseCase value) => value.name)
              .toList(growable: false),
          'connectChoice': profile.connectChoice?.name,
          'supportTypes': profile.supportTypes
              .map((SupportType value) => value.name)
              .toList(growable: false),
          'financialGoals': profile.financialGoals
              .map((FinancialGoalType value) => value.name)
              .toList(growable: false),
          'completed': profile.completed,
        },
      );
    } on DioException catch (exception) {
      throw mapDioException(exception);
    }
  }

  @override
  Future<PayaboSetupProfile?> loadSetupProfile() async {
    try {
      final response = await _apiClient.get<Map<String, dynamic>>(
        '/personal-finance/setup-profile',
      );

      return _mapProfile(response.data ?? const <String, dynamic>{});
    } on DioException catch (exception) {
      if (exception.response?.statusCode == 404) {
        return null;
      }

      throw mapDioException(exception);
    }
  }

  @override
  Future<void> clearSetupProfile() async {
    try {
      await _apiClient.delete<void>('/personal-finance/setup-profile');
    } on DioException catch (exception) {
      throw mapDioException(exception);
    }
  }

  @override
  Future<void> triggerUkAccountLink() async {}

  @override
  Future<void> triggerNigeriaAccountLink() async {}

  PayaboSetupProfile _mapProfile(Map<String, dynamic> payload) {
    return PayaboSetupProfile(
      selectedUseCases: _mapEnumList<SetupUseCase>(
        payload['selectedUseCases'],
        SetupUseCase.values,
      ),
      connectChoice: _mapOptionalEnum<SetupConnectChoice>(
        payload['connectChoice'],
        SetupConnectChoice.values,
      ),
      supportTypes: _mapEnumList<SupportType>(
        payload['supportTypes'],
        SupportType.values,
      ),
      financialGoals: _mapEnumList<FinancialGoalType>(
        payload['financialGoals'],
        FinancialGoalType.values,
      ),
      completed: payload['completed'] == true,
    );
  }

  List<T> _mapEnumList<T extends Enum>(dynamic rawValues, List<T> values) {
    if (rawValues is! List) {
      return <T>[];
    }

    final mapped = <T>[];
    for (final rawValue in rawValues) {
      final item = _mapOptionalEnum<T>(rawValue, values);
      if (item != null && !mapped.contains(item)) {
        mapped.add(item);
      }
    }

    return mapped;
  }

  T? _mapOptionalEnum<T extends Enum>(dynamic rawValue, List<T> values) {
    final normalized = rawValue?.toString().trim();
    if (normalized == null || normalized.isEmpty) {
      return null;
    }

    for (final value in values) {
      if (value.name == normalized) {
        return value;
      }
    }

    return null;
  }
}
