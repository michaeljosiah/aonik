import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../shared/theme/payabo_color_resolver.dart';
import '../../../shared/theme/payabo_spacing.dart';
import '../../../shared/widgets/payabo_button.dart';
import '../../../shared/widgets/payabo_country_flag.dart';
import 'auth_flow_scaffold.dart';
import 'onboarding_flow_state.dart';

class RegisterScreen extends ConsumerStatefulWidget {
  const RegisterScreen({super.key});

  @override
  ConsumerState<RegisterScreen> createState() => _RegisterScreenState();
}

class _RegisterScreenState extends ConsumerState<RegisterScreen> {
  bool _hasDefaultedCountry = false;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    final onboardingState = ref.watch(onboardingControllerProvider);
    final registrationCountriesValue =
        ref.watch(registrationCountryOptionsProvider);

    if (!_hasDefaultedCountry) {
      registrationCountriesValue.whenData((countries) {
        final bool hasSelectedCountry = countries.any(
          (country) => country.code == onboardingState.registrationCountryCode,
        );

        if (!hasSelectedCountry && countries.isNotEmpty) {
          _hasDefaultedCountry = true;
          WidgetsBinding.instance.addPostFrameCallback((_) {
            ref
                .read(onboardingControllerProvider.notifier)
                .setRegistrationCountry(countries.first.code);
          });
        } else if (hasSelectedCountry) {
          _hasDefaultedCountry = true;
        }
      });
    }

    final OnboardingCountry selectedCountry =
        registrationCountriesValue.maybeWhen<OnboardingCountry>(
      data: (countries) {
        for (final country in countries) {
          if (country.code == onboardingState.registrationCountryCode) {
            return country;
          }
        }
        return countries.isNotEmpty
            ? countries.first
            : onboardingState.registrationCountry;
      },
      orElse: () => onboardingState.registrationCountry,
    );
    final bool canContinue = registrationCountriesValue.maybeWhen<bool>(
      data: (countries) => countries.isNotEmpty,
      orElse: () => false,
    );
    final String? registrationMessage =
        registrationCountriesValue.when<String?>(
      data: (countries) {
        if (countries.isEmpty) {
          return 'Registration is not available for this tenant right now.';
        }
        return null;
      },
      loading: () => 'Loading available countries...',
      error: (_, __) => 'Unable to load registration countries right now.',
    );

    return AuthFlowScaffold(
      title: "Register now, it's free!",
      onClose: () => context.go('/intro'),
      useWarmBackground: true,
      footer: Text.rich(
        TextSpan(
          text: 'By registering you agree with our\n',
          style: Theme.of(context).textTheme.bodySmall?.copyWith(color: c.ink),
          children: <TextSpan>[
            TextSpan(
              text: 'Terms and Conditions',
              style: TextStyle(
                decoration: TextDecoration.underline,
                color: c.primary,
              ),
            ),
            const TextSpan(text: ' and '),
            TextSpan(
              text: 'Privacy Policy',
              style: TextStyle(
                decoration: TextDecoration.underline,
                color: c.primary,
              ),
            ),
            const TextSpan(text: '.'),
          ],
        ),
        textAlign: TextAlign.center,
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          Text(
            'Registration country',
            style: Theme.of(context)
                .textTheme
                .titleSmall
                ?.copyWith(color: c.muted),
          ),
          const SizedBox(height: PayaboSpacing.sm),
          InkWell(
            onTap: () => context.go('/auth/register/country-selection'),
            child: Container(
              padding: const EdgeInsets.symmetric(vertical: PayaboSpacing.sm),
              decoration: BoxDecoration(
                border: Border(
                  bottom: BorderSide(color: c.border),
                ),
              ),
              child: Row(
                children: <Widget>[
                  PayaboCountryFlag(country: selectedCountry),
                  const SizedBox(width: PayaboSpacing.lg),
                  Expanded(
                    child: Text(
                      selectedCountry.name,
                      style: Theme.of(context).textTheme.bodyLarge,
                    ),
                  ),
                  Icon(Icons.keyboard_arrow_down, color: c.muted),
                ],
              ),
            ),
          ),
          if (registrationMessage != null) ...<Widget>[
            const SizedBox(height: PayaboSpacing.sm),
            Text(
              registrationMessage,
              style: Theme.of(context)
                  .textTheme
                  .bodySmall
                  ?.copyWith(color: c.muted),
            ),
          ],
          const SizedBox(height: PayaboSpacing.x2),
          PayaboButton(
            label: 'Next',
            onPressed: canContinue
                ? () => context.go('/auth/register/personal-details')
                : null,
          ),
        ],
      ),
    );
  }
}
