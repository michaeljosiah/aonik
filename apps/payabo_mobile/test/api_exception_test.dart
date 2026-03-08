import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:payabo_mobile/data/api/api_exception.dart';

void main() {
  test('maps invalid grant responses to friendly credential copy', () {
    final request = RequestOptions(path: '/auth/token');
    final exception = DioException(
      requestOptions: request,
      response: Response<dynamic>(
        requestOptions: request,
        statusCode: 400,
        data: <String, dynamic>{
          'error': 'invalid_grant',
          'error_description': 'Wrong email or password.',
        },
      ),
      type: DioExceptionType.badResponse,
    );

    final error = mapDioException(exception);

    expect(error.message, 'Wrong email or password.');
    expect(error.statusCode, 401);
  });

  test('extracts embedded auth error descriptions from wrapped failures', () {
    final request = RequestOptions(path: '/auth/token');
    final exception = DioException(
      requestOptions: request,
      response: Response<dynamic>(
        requestOptions: request,
        statusCode: 403,
        data: <String, dynamic>{
          'error':
              'Auth0 token exchange failed: Forbidden {"error":"invalid_grant","error_description":"Wrong email or password."}',
        },
      ),
      type: DioExceptionType.badResponse,
    );

    final error = mapDioException(exception);

    expect(error.message, 'Wrong email or password.');
    expect(error.statusCode, 401);
  });
}
