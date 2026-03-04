import 'package:flutter_riverpod/flutter_riverpod.dart';

class MockAuthController extends StateNotifier<bool> {
  MockAuthController() : super(false);

  void signIn() {
    state = true;
  }

  void signOut() {
    state = false;
  }
}

final StateNotifierProvider<MockAuthController, bool> mockAuthProvider =
    StateNotifierProvider<MockAuthController, bool>(
  (Ref ref) => MockAuthController(),
);
