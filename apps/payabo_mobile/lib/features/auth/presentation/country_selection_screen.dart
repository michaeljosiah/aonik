import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_svg/flutter_svg.dart';
import 'package:go_router/go_router.dart';

import '../../../shared/theme/payabo_color_resolver.dart';
import '../../../shared/theme/payabo_shadows.dart';
import '../../../shared/theme/payabo_spacing.dart';
import 'onboarding_flow_state.dart';

enum CountrySelectionTarget {
  registration,
  phone,
}

class CountrySelectionScreen extends ConsumerStatefulWidget {
  const CountrySelectionScreen({
    super.key,
    required this.target,
  });

  final CountrySelectionTarget target;

  @override
  ConsumerState<CountrySelectionScreen> createState() =>
      _CountrySelectionScreenState();
}

class _CountrySelectionScreenState
    extends ConsumerState<CountrySelectionScreen> {
  final TextEditingController _searchController = TextEditingController();

  @override
  void dispose() {
    _searchController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    final query = _searchController.text.trim().toLowerCase();
    final items = onboardingCountries
        .where((country) =>
            country.name.toLowerCase().contains(query) ||
            country.code.toLowerCase().contains(query))
        .toList(growable: false);

    return Scaffold(
      backgroundColor: c.surfaceWarm,
      body: DecoratedBox(
        decoration: BoxDecoration(
          gradient: c.warmScreenGradient,
        ),
        child: SafeArea(
          child: Column(
            children: <Widget>[
              Container(
                padding: const EdgeInsets.fromLTRB(PayaboSpacing.xl,
                    PayaboSpacing.lg, PayaboSpacing.xl, PayaboSpacing.lg),
                decoration: BoxDecoration(
                  color: c.surfaceWarmElevated,
                  boxShadow: PayaboShadows.soft,
                ),
                child: Column(
                  children: <Widget>[
                    Row(
                      children: <Widget>[
                        InkWell(
                          borderRadius: BorderRadius.circular(20),
                          onTap: _goBack,
                          child: Padding(
                            padding: const EdgeInsets.all(6),
                            child: Icon(
                              Icons.arrow_back_ios_new,
                              color: c.primary,
                            ),
                          ),
                        ),
                        Expanded(
                          child: Center(
                            child: Text(
                              'Select a country',
                              style: TextStyle(
                                fontSize: 18,
                                fontWeight: FontWeight.w700,
                                color: c.ink,
                              ),
                            ),
                          ),
                        ),
                        const SizedBox(width: 28),
                      ],
                    ),
                    const SizedBox(height: PayaboSpacing.md),
                    TextField(
                      controller: _searchController,
                      onChanged: (_) => setState(() {}),
                      decoration: InputDecoration(
                        hintText: 'Search for a country',
                        prefixIcon: Icon(Icons.search, color: c.muted),
                      ),
                    ),
                  ],
                ),
              ),
              Expanded(
                child: ListView.builder(
                  padding:
                      const EdgeInsets.symmetric(horizontal: PayaboSpacing.xl),
                  itemCount: items.length,
                  itemBuilder: (context, index) {
                    final country = items[index];

                    return InkWell(
                      onTap: () => _selectCountry(country),
                      child: Container(
                        padding: const EdgeInsets.symmetric(
                            vertical: PayaboSpacing.lg),
                        decoration: BoxDecoration(
                          border: Border(
                            bottom: BorderSide(color: c.border),
                          ),
                        ),
                        child: Row(
                          children: <Widget>[
                            SvgPicture.asset(
                              country.flagAsset!,
                              width: 32,
                              height: 24,
                            ),
                            const SizedBox(width: PayaboSpacing.lg),
                            if (widget.target == CountrySelectionTarget.phone)
                              Padding(
                                padding: const EdgeInsets.only(
                                    right: PayaboSpacing.md),
                                child: Text(
                                  country.dialCode,
                                  style: Theme.of(context).textTheme.titleSmall,
                                ),
                              ),
                            Expanded(
                              child: Text(
                                country.name,
                                style: Theme.of(context).textTheme.bodyLarge,
                              ),
                            ),
                          ],
                        ),
                      ),
                    );
                  },
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }

  void _selectCountry(OnboardingCountry country) {
    final notifier = ref.read(onboardingControllerProvider.notifier);
    if (widget.target == CountrySelectionTarget.registration) {
      notifier.setRegistrationCountry(country.code);
    } else {
      notifier.setPhoneCountry(country.code);
    }

    if (context.canPop()) {
      context.pop();
      return;
    }

    context.go(widget.target == CountrySelectionTarget.registration
        ? '/auth/register'
        : '/auth/register/contact-details');
  }

  void _goBack() {
    if (context.canPop()) {
      context.pop();
      return;
    }

    context.go(widget.target == CountrySelectionTarget.registration
        ? '/auth/register'
        : '/auth/register/contact-details');
  }
}
