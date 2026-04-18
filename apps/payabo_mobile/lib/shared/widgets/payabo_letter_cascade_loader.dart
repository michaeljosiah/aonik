import 'package:flutter/material.dart';
import 'package:google_fonts/google_fonts.dart';

import '../theme/payabo_color_resolver.dart';

/// Port of loader #16 ("Letter fade — cascade in, cascade out") from the
/// Payabo Loaders.html gallery.
///
/// Each glyph of `Payabo\u00B7` pulses from 15% to 100% opacity and back
/// over the full loop, staggered by 80 ms per letter so the brightness
/// sweeps from left to right. Middle dot uses the brand orange; the rest
/// use `c.textPrimary` so it flips correctly in dark theme.
class PayaboLetterCascadeLoader extends StatefulWidget {
  const PayaboLetterCascadeLoader({
    super.key,
    this.fontSize = 48,
    this.duration = const Duration(milliseconds: 2400),
  });

  final double fontSize;
  final Duration duration;

  @override
  State<PayaboLetterCascadeLoader> createState() =>
      _PayaboLetterCascadeLoaderState();
}

class _PayaboLetterCascadeLoaderState extends State<PayaboLetterCascadeLoader>
    with SingleTickerProviderStateMixin {
  static const String _wordmark = 'Payabo\u00B7';
  static const double _baseOpacity = 0.15;
  static const double _peakOpacity = 1.0;
  static const Duration _perLetterStagger = Duration(milliseconds: 80);

  // CSS keyframes: opacity 0.15 at 0%/100%, 1.0 at 40%/55%, ease-in-out.
  static const double _riseEnd = 0.40;
  static const double _holdEnd = 0.55;

  late final AnimationController _controller;

  @override
  void initState() {
    super.initState();
    _controller = AnimationController(vsync: this, duration: widget.duration)
      ..repeat();
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  double _opacityAtLocalTime(double t) {
    if (t < _riseEnd) {
      final double eased = Curves.easeInOut.transform(t / _riseEnd);
      return _baseOpacity + (_peakOpacity - _baseOpacity) * eased;
    }
    if (t <= _holdEnd) return _peakOpacity;
    final double eased =
        Curves.easeInOut.transform((t - _holdEnd) / (1.0 - _holdEnd));
    return _peakOpacity - (_peakOpacity - _baseOpacity) * eased;
  }

  double _opacityForLetter(int index, double controllerValue) {
    final double delayFraction =
        (_perLetterStagger * index).inMilliseconds /
            widget.duration.inMilliseconds;
    double localT = controllerValue - delayFraction;
    if (localT < 0) localT += 1;
    return _opacityAtLocalTime(localT);
  }

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    final double fs = widget.fontSize;
    const int lastIndex = _wordmark.length - 1;

    return AnimatedBuilder(
      animation: _controller,
      builder: (BuildContext context, Widget? child) {
        final double v = _controller.value;
        return RichText(
          text: TextSpan(
            style: GoogleFonts.openSans(
              fontSize: fs,
              fontWeight: FontWeight.w800,
              height: 1.0,
              letterSpacing: fs * 0.01,
            ),
            children: <InlineSpan>[
              for (int i = 0; i < _wordmark.length; i++)
                TextSpan(
                  text: _wordmark[i],
                  style: TextStyle(
                    color: (i == lastIndex ? c.primary : c.textPrimary)
                        .withValues(alpha: _opacityForLetter(i, v)),
                  ),
                ),
            ],
          ),
        );
      },
    );
  }
}
