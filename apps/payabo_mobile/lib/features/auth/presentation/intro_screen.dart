import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../../../shared/theme/payabo_colors.dart';
import '../../../shared/theme/payabo_spacing.dart';
import '../../../shared/widgets/payabo_button.dart';

class IntroScreen extends StatefulWidget {
  const IntroScreen({super.key});

  @override
  State<IntroScreen> createState() => _IntroScreenState();
}

class _IntroScreenState extends State<IntroScreen> {
  final PageController _pageController = PageController();
  int _activeIndex = 0;

  static const List<_IntroSlide> _slides = <_IntroSlide>[
    _IntroSlide(
      imageAsset: 'assets/images/slider-img-01.png',
      titleLineOne: 'PAY YOUR BILLS',
      titleLineTwo: 'IN ONE PLACE',
    ),
    _IntroSlide(
      imageAsset: 'assets/images/slider-img-02.png',
      titleLineOne: 'SUPPORT YOUR',
      titleLineTwo: 'LOVED ONES',
    ),
    _IntroSlide(
      imageAsset: 'assets/images/slider-img-03.png',
      titleLineOne: 'KEEP TRACK OF',
      titleLineTwo: 'YOUR SPENDING',
    ),
  ];

  @override
  void dispose() {
    _pageController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: PayaboColors.white,
      body: SafeArea(
        child: Column(
          children: <Widget>[
            Expanded(
              child: Column(
                children: <Widget>[
                  Expanded(
                    child: PageView.builder(
                      controller: _pageController,
                      itemCount: _slides.length,
                      onPageChanged: (index) {
                        setState(() {
                          _activeIndex = index;
                        });
                      },
                      itemBuilder: (context, index) {
                        return _IntroSlideView(slide: _slides[index]);
                      },
                    ),
                  ),
                  const SizedBox(height: PayaboSpacing.lg),
                  Row(
                    mainAxisAlignment: MainAxisAlignment.center,
                    children: List<Widget>.generate(
                      _slides.length,
                      (index) => _SlideDot(isActive: index == _activeIndex),
                    ),
                  ),
                ],
              ),
            ),
            Padding(
              padding: const EdgeInsets.fromLTRB(PayaboSpacing.xl,
                  PayaboSpacing.x2, PayaboSpacing.xl, PayaboSpacing.xl),
              child: Row(
                children: <Widget>[
                  Expanded(
                    child: PayaboButton(
                      label: 'Login',
                      onPressed: () => context.go('/auth/login'),
                    ),
                  ),
                  const SizedBox(width: PayaboSpacing.md),
                  Expanded(
                    child: PayaboButton(
                      label: 'Register',
                      variant: PayaboButtonVariant.secondary,
                      onPressed: () => context.go('/auth/register'),
                    ),
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _IntroSlide {
  const _IntroSlide({
    required this.imageAsset,
    required this.titleLineOne,
    required this.titleLineTwo,
  });

  final String imageAsset;
  final String titleLineOne;
  final String titleLineTwo;
}

class _IntroSlideView extends StatelessWidget {
  const _IntroSlideView({required this.slide});

  final _IntroSlide slide;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.fromLTRB(PayaboSpacing.x2, PayaboSpacing.x2,
          PayaboSpacing.x2, PayaboSpacing.lg),
      child: Column(
        mainAxisAlignment: MainAxisAlignment.center,
        children: <Widget>[
          Image.asset(
            slide.imageAsset,
            fit: BoxFit.contain,
          ),
          const SizedBox(height: PayaboSpacing.xl),
          Text(
            slide.titleLineOne,
            textAlign: TextAlign.center,
            style: Theme.of(context).textTheme.headlineLarge?.copyWith(
                  fontSize: 32,
                  fontWeight: FontWeight.w300,
                ),
          ),
          Text(
            slide.titleLineTwo,
            textAlign: TextAlign.center,
            style: Theme.of(context).textTheme.headlineLarge?.copyWith(
                  fontSize: 32,
                  fontWeight: FontWeight.w700,
                  color: PayaboColors.primary,
                ),
          ),
        ],
      ),
    );
  }
}

class _SlideDot extends StatelessWidget {
  const _SlideDot({required this.isActive});

  final bool isActive;

  @override
  Widget build(BuildContext context) {
    return AnimatedContainer(
      duration: const Duration(milliseconds: 220),
      width: isActive ? 24 : 12,
      height: 12,
      margin: const EdgeInsets.symmetric(horizontal: 5),
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(25),
        border: Border.all(
            color: isActive ? PayaboColors.muted : PayaboColors.border,
            width: 3),
        color: isActive ? PayaboColors.muted : PayaboColors.white,
      ),
    );
  }
}
