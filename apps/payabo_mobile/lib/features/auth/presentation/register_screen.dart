import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_svg/flutter_svg.dart';
import 'package:go_router/go_router.dart';

import '../../../shared/theme/payabo_borders.dart';
import '../../../shared/theme/payabo_colors.dart';
import '../../../shared/theme/payabo_spacing.dart';
import '../../../shared/widgets/payabo_button.dart';
import 'auth_flow_scaffold.dart';
import 'onboarding_flow_state.dart';

class RegisterScreen extends ConsumerWidget {
  const RegisterScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final onboardingState = ref.watch(onboardingControllerProvider);
    final selectedCountry = onboardingState.registrationCountry;

    return AuthFlowScaffold(
      title: "Register now, it's free!",
      onClose: () => context.go('/intro'),
      footer: Text(
        'By registering you agree with our\nTerms and Conditions and Privacy Policy.',
        textAlign: TextAlign.center,
        style: Theme.of(context)
            .textTheme
            .bodySmall
            ?.copyWith(color: PayaboColors.ink),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          Text(
            'Registration country',
            style: Theme.of(context)
                .textTheme
                .titleSmall
                ?.copyWith(color: PayaboColors.muted),
          ),
          const SizedBox(height: PayaboSpacing.sm),
          InkWell(
            onTap: () => context.go('/auth/register/country-selection'),
            child: Container(
              padding: const EdgeInsets.symmetric(vertical: PayaboSpacing.sm),
              decoration: const BoxDecoration(
                border: Border(
                  bottom: PayaboBorders.defaultBorder,
                ),
              ),
              child: Row(
                children: <Widget>[
                  SvgPicture.asset(
                    selectedCountry.flagAsset,
                    width: 32,
                    height: 24,
                  ),
                  const SizedBox(width: PayaboSpacing.lg),
                  Expanded(
                    child: Text(
                      selectedCountry.name,
                      style: Theme.of(context).textTheme.bodyLarge,
                    ),
                  ),
                  const Icon(Icons.keyboard_arrow_down,
                      color: PayaboColors.muted),
                ],
              ),
            ),
          ),
          const SizedBox(height: PayaboSpacing.x2),
          PayaboButton(
            label: 'Next',
            onPressed: () => context.go('/auth/register/personal-details'),
          ),
        ],
      ),
    );
  }
}
