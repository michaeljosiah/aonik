import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_riverpod/legacy.dart';

/// Runtime session flag for Payabo demo access.
///
/// This is only turned on when startup cannot reach the API or when the user
/// explicitly chooses to continue in demo mode from the login screen.
final StateProvider<bool> isDemoProvider = StateProvider<bool>(
  (Ref ref) => false,
);
