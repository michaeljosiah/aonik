import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../app/demo/demo_mode.dart';
import '../../../shared/theme/payabo_color_resolver.dart';
import '../../../shared/theme/payabo_colors.dart';
import '../../../shared/theme/payabo_spacing.dart';
import '../../../shared/widgets/payabo_button.dart';
import 'auth_flow_scaffold.dart';

class IntroScreen extends ConsumerStatefulWidget {
  const IntroScreen({super.key});

  @override
  ConsumerState<IntroScreen> createState() => _IntroScreenState();
}

class _IntroScreenState extends ConsumerState<IntroScreen> {
  final PageController _pageController = PageController();
  Timer? _autoScrollTimer;
  int _activeIndex = 0;

  static const Duration _autoScrollDelay = Duration(seconds: 4);
  static const Duration _autoScrollDuration = Duration(milliseconds: 450);

  // TODO: Replace illustration assets with photo-real images and change
  // BoxFit.contain → BoxFit.cover for full-bleed backgrounds.
  static const List<_IntroSlide> _slides = <_IntroSlide>[
    _IntroSlide(
      imageAsset: 'assets/images/slider-img-01.png',
      subtitle: 'Pay your bills',
      headline: 'All your bills, paid\nin one place with ease.',
    ),
    _IntroSlide(
      imageAsset: 'assets/images/slider-img-02.png',
      subtitle: 'Support loved ones',
      headline: 'Send money to friends\nand family, anywhere.',
    ),
    _IntroSlide(
      imageAsset: 'assets/images/slider-img-03.png',
      subtitle: 'Track spending',
      headline: 'Keep track of your\nspending and reach\nyour goals.',
    ),
  ];

  @override
  void initState() {
    super.initState();
    _scheduleAutoScroll();
  }

  @override
  void dispose() {
    _autoScrollTimer?.cancel();
    _pageController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final slide = _slides[_activeIndex];
    final screenHeight = MediaQuery.of(context).size.height;
    final isDemo = ref.watch(isDemoProvider);
    final c = context.colors;

    return Scaffold(
      extendBodyBehindAppBar: true,
      body: Stack(
        fit: StackFit.expand,
        children: <Widget>[
          // ── Full-screen paged images ──────────────────────────────
          PageView.builder(
            controller: _pageController,
            itemCount: _slides.length,
            onPageChanged: (index) {
              setState(() => _activeIndex = index);
              _scheduleAutoScroll();
            },
            itemBuilder: (context, index) {
              return Image.asset(
                _slides[index].imageAsset,
                fit: BoxFit.cover,
                width: double.infinity,
                height: double.infinity,
              );
            },
          ),

          // ── Bottom gradient overlay ───────────────────────────────
          Positioned(
            left: 0,
            right: 0,
            bottom: 0,
            height: screenHeight * 0.44,
            child: IgnorePointer(
              child: DecoratedBox(
                decoration: BoxDecoration(
                  gradient: LinearGradient(
                    begin: Alignment.topCenter,
                    end: Alignment.bottomCenter,
                    stops: const <double>[0.0, 0.30, 1.0],
                    colors: <Color>[
                      Colors.black.withValues(alpha: 0.0),
                      Colors.black.withValues(alpha: 0.70),
                      Colors.black.withValues(alpha: 0.92),
                    ],
                  ),
                ),
              ),
            ),
          ),

          // ── Bottom content (text + dots + buttons) ────────────────
          Positioned(
            left: PayaboSpacing.x2,
            right: PayaboSpacing.x2,
            bottom: 0,
            child: SafeArea(
              top: false,
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                mainAxisSize: MainAxisSize.min,
                children: <Widget>[
                  if (isDemo) ...<Widget>[
                    const AuthModeNoticeCard(
                      title: 'Demo mode is active',
                      message:
                          'Live sign-in and account creation are unavailable right now. Continue to login and use Access in demo mode to open the guided experience.',
                      icon: Icons.wifi_off_rounded,
                    ),
                    const SizedBox(height: PayaboSpacing.xl),
                  ],
                  // Subtitle
                  Text(
                    slide.subtitle,
                    style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                          color: PayaboColors.brandPrimary,
                          fontWeight: FontWeight.w600,
                          letterSpacing: 0.3,
                        ),
                  ),
                  const SizedBox(height: PayaboSpacing.sm),

                  // Headline
                  Text(
                    slide.headline,
                    style: Theme.of(context).textTheme.headlineLarge?.copyWith(
                          color: Colors.white,
                          fontWeight: FontWeight.w700,
                          fontSize: 28,
                          height: 1.25,
                        ),
                  ),
                  const SizedBox(height: PayaboSpacing.x2),

                  // Page indicator dots
                  Row(
                    mainAxisAlignment: MainAxisAlignment.center,
                    children: List<Widget>.generate(
                      _slides.length,
                      (index) => _PageDot(isActive: index == _activeIndex),
                    ),
                  ),
                  const SizedBox(height: PayaboSpacing.x3),

                  // Log in
                  PayaboButton(
                    label: 'Log in',
                    onPressed: () => context.go('/auth/login'),
                  ),
                  const SizedBox(height: PayaboSpacing.md),

                  // Create an account
                  PayaboButton(
                    label: 'Create an account',
                    variant: PayaboButtonVariant.link,
                    onPressed:
                        isDemo ? null : () => context.go('/auth/register'),
                  ),
                  if (isDemo) ...<Widget>[
                    const SizedBox(height: PayaboSpacing.sm),
                    Text(
                      'Account creation is unavailable in demo mode.',
                      style: Theme.of(context).textTheme.bodySmall?.copyWith(
                            color: c.surfaceWarm,
                          ),
                      textAlign: TextAlign.center,
                    ),
                  ],
                  const SizedBox(height: PayaboSpacing.lg),
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }

  void _scheduleAutoScroll() {
    _autoScrollTimer?.cancel();
    _autoScrollTimer = Timer(_autoScrollDelay, _goToNextSlide);
  }

  void _goToNextSlide() {
    if (!mounted || !_pageController.hasClients) {
      return;
    }

    final nextIndex = (_activeIndex + 1) % _slides.length;
    _pageController.animateToPage(
      nextIndex,
      duration: _autoScrollDuration,
      curve: Curves.easeInOut,
    );
  }
}

// ── Data model ──────────────────────────────────────────────────────────

class _IntroSlide {
  const _IntroSlide({
    required this.imageAsset,
    required this.subtitle,
    required this.headline,
  });

  final String imageAsset;
  final String subtitle;
  final String headline;
}

// ── Page indicator dot ──────────────────────────────────────────────────

class _PageDot extends StatelessWidget {
  const _PageDot({required this.isActive});

  final bool isActive;

  @override
  Widget build(BuildContext context) {
    return AnimatedContainer(
      duration: const Duration(milliseconds: 200),
      width: isActive ? 10 : 8,
      height: isActive ? 10 : 8,
      margin: const EdgeInsets.symmetric(horizontal: 4),
      decoration: BoxDecoration(
        shape: BoxShape.circle,
        color: isActive ? Colors.white : Colors.white.withValues(alpha: 0.35),
      ),
    );
  }
}
