import 'dart:ui' as ui;

import 'package:flutter/material.dart';

import '../../../../shared/theme/payabo_color_resolver.dart';
import '../../../../shared/theme/payabo_spacing.dart';

/// Displays the current AI message with a typewriter + blur-into-existence
/// animation, reinforcing the feeling that "Payabo is thinking and
/// preparing my financial assistant."
///
/// ## Animation breakdown
///
/// 1. **Typewriter reveal** — characters appear one by one (~20ms/char,
///    clamped to 600–2500ms total).
/// 2. **Trailing opacity fade** — the last [_fadeWindow] characters ramp
///    from 30% to 100% opacity, simulating a "materialising" edge.
/// 3. **Initial gaussian blur** — the whole text starts blurred
///    (sigma [_initialBlurSigma]) and clears to sharp over 500ms.
/// 4. **Helper text fade-in** — after the typewriter completes, helper
///    text fades in over 400ms.
///
/// When [stepKey] changes, a new [_TypewriterContent] is created (via
/// [ValueKey]), restarting all animations from scratch.
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
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: PayaboSpacing.x2),
      child: _TypewriterContent(
        key: ValueKey<String>(stepKey),
        message: message,
        helperText: helperText,
      ),
    );
  }
}

// ── Typewriter content ────────────────────────────────────────

class _TypewriterContent extends StatefulWidget {
  const _TypewriterContent({
    super.key,
    required this.message,
    this.helperText,
  });

  final String message;
  final String? helperText;

  @override
  State<_TypewriterContent> createState() => _TypewriterContentState();
}

class _TypewriterContentState extends State<_TypewriterContent>
    with TickerProviderStateMixin {
  late final AnimationController _typewriter;
  late final AnimationController _blur;
  late final AnimationController _helperFade;

  /// Number of trailing characters with ramped opacity.
  static const int _fadeWindow = 5;

  /// Milliseconds per character (before clamping).
  static const int _msPerChar = 20;

  /// Min / max total typewriter duration.
  static const int _minDurationMs = 600;
  static const int _maxDurationMs = 2500;

  /// Initial gaussian blur sigma.
  static const double _initialBlurSigma = 4.0;

  /// Blur clears to zero over this duration.
  static const Duration _blurDuration = Duration(milliseconds: 500);

  /// Helper text fade-in duration.
  static const Duration _helperDuration = Duration(milliseconds: 400);

  @override
  void initState() {
    super.initState();

    final totalMs =
        (widget.message.characters.length * _msPerChar).clamp(_minDurationMs, _maxDurationMs);

    _typewriter = AnimationController(
      vsync: this,
      duration: Duration(milliseconds: totalMs),
    );

    _blur = AnimationController(
      vsync: this,
      duration: _blurDuration,
    );

    _helperFade = AnimationController(
      vsync: this,
      duration: _helperDuration,
    );

    // Start the entrance.
    _typewriter.forward();
    _blur.forward();

    // Fade helper text in after the main message finishes typing.
    if (widget.helperText != null) {
      _typewriter.addStatusListener(_onTypewriterDone);
    }
  }

  void _onTypewriterDone(AnimationStatus status) {
    if (status == AnimationStatus.completed && mounted) {
      _helperFade.forward();
    }
  }

  @override
  void dispose() {
    _typewriter.removeStatusListener(_onTypewriterDone);
    _typewriter.dispose();
    _blur.dispose();
    _helperFade.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    final textTheme = Theme.of(context).textTheme;

    final messageStyle = textTheme.headlineMedium?.copyWith(
      color: c.headerTitle,
      height: 1.4,
    );

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      mainAxisSize: MainAxisSize.min,
      children: <Widget>[
        // Main message — typewriter + blur
        AnimatedBuilder(
          animation: Listenable.merge(<Listenable>[_typewriter, _blur]),
          builder: (BuildContext context, Widget? _) {
            return _buildTypewriterText(messageStyle);
          },
        ),

        // Helper text — fades in after the typewriter completes
        if (widget.helperText != null) ...<Widget>[
          const SizedBox(height: PayaboSpacing.md),
          FadeTransition(
            opacity: CurvedAnimation(
              parent: _helperFade,
              curve: Curves.easeOut,
            ),
            child: Text(
              widget.helperText!,
              style: textTheme.bodyMedium?.copyWith(
                color: c.textSubtleWarm,
                fontStyle: FontStyle.italic,
              ),
            ),
          ),
        ],
      ],
    );
  }

  // ── Grapheme-aware character list ─────────────────────────

  /// Cache grapheme clusters so we don't re-split on every frame.
  late final List<String> _graphemes =
      widget.message.characters.toList(growable: false);

  // ── Typewriter text builder ─────────────────────────────────

  Widget _buildTypewriterText(TextStyle? style) {
    final total = _graphemes.length;
    final charIndex =
        (_typewriter.value * total).ceil().clamp(0, total);

    if (charIndex == 0) return const SizedBox.shrink();

    final baseColor = style?.color ?? Colors.white;

    // Where the trailing opacity fade starts.
    final fadeStart = (charIndex - _fadeWindow).clamp(0, total);

    final spans = <InlineSpan>[];

    // Fully-revealed characters (sharp, full opacity).
    if (fadeStart > 0) {
      spans.add(TextSpan(
        text: _graphemes.sublist(0, fadeStart).join(),
        style: style,
      ));
    }

    // Trailing fade window — characters ramp from 30% to 100% opacity,
    // giving a "materialising" edge to the typewriter.
    for (int i = fadeStart; i < charIndex; i++) {
      final t = (i - fadeStart + 1) / _fadeWindow;
      spans.add(TextSpan(
        text: _graphemes[i],
        style: style?.copyWith(
          color: baseColor.withValues(
            alpha: (0.3 + 0.7 * t).clamp(0.0, 1.0),
          ),
        ),
      ));
    }

    Widget text = Text.rich(TextSpan(children: spans));

    // Gaussian blur: starts at [_initialBlurSigma] and eases to zero.
    // Applied to the whole text during the first [_blurDuration] only.
    final blurSigma = _initialBlurSigma *
        (1.0 - Curves.easeOut.transform(_blur.value));

    if (blurSigma > 0.15) {
      text = ImageFiltered(
        imageFilter: ui.ImageFilter.blur(
          sigmaX: blurSigma,
          sigmaY: blurSigma,
          tileMode: TileMode.decal,
        ),
        child: text,
      );
    }

    return text;
  }
}
