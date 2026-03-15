import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../router/app_router.dart' show rootNavigatorKey;
import 'api_error_notifier.dart';

/// Wraps the app's widget tree and shows a dialog whenever an [ApiError] is
/// reported to [apiErrorNotifierProvider].
///
/// Errors queue up — each is shown in turn after the previous one is dismissed.
/// Place this widget inside [MaterialApp.builder]. It uses the [rootNavigatorKey]
/// to show dialogs because [MaterialApp.builder] sits above the [Navigator] in
/// the widget tree, so the widget's own [BuildContext] cannot find it.
class ApiErrorListener extends ConsumerStatefulWidget {
  const ApiErrorListener({required this.child, super.key});

  final Widget child;

  @override
  ConsumerState<ApiErrorListener> createState() => _ApiErrorListenerState();
}

class _ApiErrorListenerState extends ConsumerState<ApiErrorListener> {
  bool _isShowing = false;

  void _showNext() {
    if (_isShowing || !mounted) return;

    final errors = ref.read(apiErrorNotifierProvider);
    if (errors.isEmpty) return;

    final navigatorContext = rootNavigatorKey.currentContext;
    if (navigatorContext == null) return;

    _isShowing = true;

    showDialog<void>(
      context: navigatorContext,
      barrierDismissible: false,
      builder: (_) => _ApiErrorDialog(error: errors.first),
    ).then((_) {
      ref.read(apiErrorNotifierProvider.notifier).dismiss();
      _isShowing = false;
      _showNext();
    });
  }

  @override
  Widget build(BuildContext context) {
    ref.listen<List<ApiError>>(apiErrorNotifierProvider, (_, next) {
      if (next.isNotEmpty) _showNext();
    });

    return widget.child;
  }
}

class _ApiErrorDialog extends StatelessWidget {
  const _ApiErrorDialog({required this.error});

  final ApiError error;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return AlertDialog(
      title: const Text('Something went wrong'),
      content: Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(error.message),
          if (error.detail != null) ...[
            const SizedBox(height: 8),
            Text(
              error.detail!,
              style: theme.textTheme.bodySmall?.copyWith(
                color: theme.colorScheme.onSurface.withValues(alpha: 0.6),
              ),
            ),
          ],
        ],
      ),
      actions: [
        TextButton(
          onPressed: () => Navigator.of(context).pop(),
          child: const Text('OK'),
        ),
      ],
    );
  }
}
