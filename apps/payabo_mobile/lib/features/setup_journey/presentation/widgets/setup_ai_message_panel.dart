import 'package:flutter/material.dart';

import '../../../../shared/theme/payabo_color_resolver.dart';
import '../../../../shared/theme/payabo_spacing.dart';
import '../../../../shared/widgets/payabo_typewriter_text.dart';

/// Displays the current AI message with a typewriter + blur-into-existence
/// animation, reinforcing the feeling that "Payabo is thinking and
/// preparing my financial assistant."
///
/// This is a thin wrapper around [PayaboTypewriterText] that applies the
/// setup journey's standard padding and theme colours.
///
/// When [stepKey] changes, a new animation cycle is triggered via the
/// underlying [PayaboTypewriterText.animationKey].
class SetupAiMessagePanel extends StatelessWidget {
  const SetupAiMessagePanel({
    super.key,
    required this.message,
    this.helperText,
    required this.stepKey,
  });

  /// The AI message copy to display.
  final String message;

  /// Optional secondary text below the main message.
  final String? helperText;

  /// A unique key for the current step, used to trigger recreation
  /// of the typewriter animation.
  final String stepKey;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    final textTheme = Theme.of(context).textTheme;

    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: PayaboSpacing.x2),
      child: PayaboTypewriterText(
        animationKey: stepKey,
        message: message,
        helperText: helperText,
        messageStyle: textTheme.headlineMedium?.copyWith(
          color: c.headerTitle,
          height: 1.4,
        ),
        helperStyle: textTheme.bodyMedium?.copyWith(
          color: c.textSubtleWarm,
          fontStyle: FontStyle.italic,
        ),
      ),
    );
  }
}
