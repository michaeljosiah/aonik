import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:payabo_mobile/data/repositories/live_profile_repository.dart';
import 'package:payabo_mobile/data/repositories/profile_repository.dart';
import 'package:payabo_mobile/data/repositories/repository_providers.dart';
import 'package:payabo_mobile/features/profile/presentation/profile_state.dart';
import 'package:payabo_mobile/mock/mock_behavior.dart';
import 'package:payabo_mobile/mock/repositories/mock_profile_repository.dart';

void main() {
  test('email change sends only the new email and accepts an empty 202', () async {
    final client = Dio();
    addTearDown(client.close);
    RequestOptions? sent;
    client.interceptors.add(InterceptorsWrapper(
      onRequest: (options, handler) {
        sent = options;
        handler.resolve(Response<void>(requestOptions: options, statusCode: 202));
      },
    ));
    final repository = LiveProfileRepository(apiClient: client);

    await repository.requestEmailChange(newEmail: ' new@example.com ');

    expect(sent!.method, 'PUT');
    expect(sent!.path, '/profiles/customers/me/email');
    expect(sent!.data, <String, dynamic>{'newEmail': 'new@example.com'});
  });

  test('request acceptance preserves profile until a confirmed profile is reloaded', () async {
    MockBehavior.skipDelayForTest = true;
    addTearDown(() => MockBehavior.skipDelayForTest = false);
    final repository = MockProfileRepository();
    final container = ProviderContainer(overrides: [
      profileRepositoryProvider.overrideWithValue(repository),
    ]);
    addTearDown(container.dispose);
    final controller = container.read(profileCoreProvider.notifier);
    await controller.ensureLoaded();
    final original = container.read(profileCoreProvider);

    await controller.requestEmailChange(newEmail: 'new@example.com');

    expect(container.read(profileCoreProvider).email, original.email);
    expect((await repository.getProfile()).email, original.email);
    await repository.updateProfile(UserProfile(
      firstName: original.firstName,
      lastName: original.lastName,
      email: 'new@example.com',
      phone: original.phone,
      countryCode: original.countryCode,
      photoUrl: original.photoUrl,
    ));
    await controller.reload();
    expect(container.read(profileCoreProvider).email, 'new@example.com');
  });
}
