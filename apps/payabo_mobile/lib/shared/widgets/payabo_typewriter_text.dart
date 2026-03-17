import 'dart:ui' as ui;

import 'package:flutter/material.dart';

/// A reusable typewriter + blur-into-existence text animation.
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
/// Provide a unique [animationKey] to restart animations from scratch
/// when the message changes.
class PayaboTypewriterText extends StatefulWidget {
  const PayaboTypewriterText({
    super.key,
    required this.message,
    this.helperText,
    required this.animationKey,
    this.messageStyle,
    this.helperStyle,
    this.helperSpacing = 12.0,
  });

  /// The main text to reveal with the typewriter animation.
  final String message;

  /// Optional secondary text below the main message (fades in after typing).
  final String? helperText;

  /// A unique key for the current content. When this changes, the internal
  /// [_TypewriterContentState] is recreated and all animations restart.
  final String animationKey;

  /// Style for the main typewriter text. If null, [headlineMedium] is used.
  final TextStyle? messageStyle;

  /// Style for the helper text. If null, [bodyMedium] is used.
  final TextStyle? helperStyle;

  /// Vertical spacing between message and helper text.
  final double helperSpacing;

  @override
  State<PayaboTypewriterText> createState() => _PayaboTypewriterTextState();
}

class _PayaboTypewriterTextState extends State<PayaboTypewriterText> {
  @override
  Widget build(BuildContext context) {
    return _TypewriterContent(
      key: ValueKey<String>(widget.animationKey),
      message: widget.message,
      helperText: widget.helperText,
      messageStyle: widget.messageStyle ??
          Theme.of(context).textTheme.headlineMedium?.copyWith(
                height: 1.4,
              ),
      helperStyle: widget.helperStyle ??
          Theme.of(context).textTheme.bodyMedium?.copyWith(
                fontStyle: FontStyle.italic,
              ),
      helperSpacing: widget.helperSpacing,
    );
  }
}

// ── Typewriter content (internal) ─────────────────────────

class _TypewriterContent extends StatefulWidget {
  const _TypewriterContent({
    super.key,
    required this.message,
    this.helperText,
    this.messageStyle,
    this.helperStyle,
    this.helperSpacing = 12.0,
  });

  final String message;
  final String? helperText;
  final TextStyle? messageStyle;
  final TextStyle? helperStyle;
  final double helperSpacing;

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

    final totalMs = (widget.message.characters.length * _msPerChar)
        .clamp(_minDurationMs, _maxDurationMs);

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
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      mainAxisSize: MainAxisSize.min,
      children: <Widget>[
        // Main message — typewriter + blur
        AnimatedBuilder(
          animation: Listenable.merge(<Listenable>[_typewriter, _blur]),
          builder: (BuildContext context, Widget? _) {
            return _buildTypewriterText(widget.messageStyle);
          },
        ),

        // Helper text — fades in after the typewriter completes
        if (widget.helperText != null) ...<Widget>[
          SizedBox(height: widget.helperSpacing),
          FadeTransition(
            opacity: CurvedAnimation(
              parent: _helperFade,
              curve: Curves.easeOut,
            ),
            child: Text(
              widget.helperText!,
              style: widget.helperStyle,
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
    final charIndex = (_typewriter.value * total).ceil().clamp(0, total);

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
    final blurSigma =
        _initialBlurSigma * (1.0 - Curves.easeOut.transform(_blur.value));

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
