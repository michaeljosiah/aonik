import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../shared/theme/payabo_spacing.dart';
import '../../../shared/widgets/payabo_button.dart';
import '../../../shared/widgets/payabo_text_field.dart';
import 'auth_flow_scaffold.dart';
import 'onboarding_flow_state.dart';

class PersonalDetailsScreen extends ConsumerStatefulWidget {
  const PersonalDetailsScreen({super.key});

  @override
  ConsumerState<PersonalDetailsScreen> createState() =>
      _PersonalDetailsScreenState();
}

class _PersonalDetailsScreenState extends ConsumerState<PersonalDetailsScreen> {
  late final TextEditingController _firstNameController;
  late final TextEditingController _lastNameController;

  @override
  void initState() {
    super.initState();
    final onboarding = ref.read(onboardingControllerProvider);
    _firstNameController = TextEditingController(text: onboarding.firstName);
    _lastNameController = TextEditingController(text: onboarding.lastName);
  }

  @override
  void dispose() {
    _firstNameController.dispose();
    _lastNameController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final canContinue = _firstNameController.text.trim().isNotEmpty &&
        _lastNameController.text.trim().isNotEmpty;

    return AuthFlowScaffold(
      title: 'Personal details',
      onBack: () => context.go('/auth/register'),
      useWarmBackground: true,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          PayaboTextField(
            label: 'First name',
            variant: PayaboInputVariant.floating,
            controller: _firstNameController,
            onChanged: (value) {
              ref
                  .read(onboardingControllerProvider.notifier)
                  .setFirstName(value);
              setState(() {});
            },
          ),
          const SizedBox(height: PayaboSpacing.md),
          PayaboTextField(
            label: 'Last name',
            variant: PayaboInputVariant.floating,
            controller: _lastNameController,
            onChanged: (value) {
              ref
                  .read(onboardingControllerProvider.notifier)
                  .setLastName(value);
              setState(() {});
            },
          ),
          const SizedBox(height: PayaboSpacing.xl),
          PayaboButton(
            label: 'Next',
            onPressed: canContinue
                ? () => context.go('/auth/register/contact-details')
                : null,
          ),
        ],
      ),
    );
  }
}
