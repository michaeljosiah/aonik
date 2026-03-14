import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_svg/flutter_svg.dart';
import 'package:go_router/go_router.dart';

import '../../../shared/theme/payabo_color_resolver.dart';
import '../../../shared/theme/payabo_spacing.dart';
import '../../../shared/widgets/payabo_button.dart';
import 'auth_flow_scaffold.dart';
import 'onboarding_flow_state.dart';

class ContactDetailsScreen extends ConsumerStatefulWidget {
  const ContactDetailsScreen({super.key});

  @override
  ConsumerState<ContactDetailsScreen> createState() =>
      _ContactDetailsScreenState();
}

class _ContactDetailsScreenState extends ConsumerState<ContactDetailsScreen> {
  late final TextEditingController _phoneController;

  @override
  void initState() {
    super.initState();
    _phoneController = TextEditingController(
      text: ref.read(onboardingControllerProvider).mobileNumber,
    );
  }

  @override
  void dispose() {
    _phoneController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    final onboarding = ref.watch(onboardingControllerProvider);
    final phoneCountry = onboarding.phoneCountry;
    final canVerify =
        _phoneController.text.trim().replaceAll(RegExp(r'\D'), '').length >= 6;

    return AuthFlowScaffold(
      title: 'Contact details',
      onBack: () => context.go('/auth/register/personal-details'),
      useWarmBackground: true,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          Text(
            'Mobile number',
            style: Theme.of(context)
                .textTheme
                .titleSmall
                ?.copyWith(color: c.muted),
          ),
          const SizedBox(height: PayaboSpacing.sm),
          Row(
            crossAxisAlignment: CrossAxisAlignment.end,
            children: <Widget>[
              InkWell(
                onTap: () =>
                    context.go('/auth/register/phone-country-selection'),
                child: Container(
                  padding: const EdgeInsets.only(bottom: 8, right: 12, top: 6),
                  decoration: BoxDecoration(
                    border: Border(
                      bottom: BorderSide(color: c.border),
                    ),
                  ),
                  child: Row(
                    children: <Widget>[
                      SvgPicture.asset(
                        phoneCountry.flagAsset!,
                        width: 26,
                        height: 20,
                      ),
                      const SizedBox(width: PayaboSpacing.sm),
                      Text(
                        phoneCountry.dialCode,
                        style: Theme.of(context).textTheme.titleSmall,
                      ),
                    ],
                  ),
                ),
              ),
              const SizedBox(width: PayaboSpacing.md),
              Expanded(
                child: TextField(
                  controller: _phoneController,
                  keyboardType: TextInputType.phone,
                  style: Theme.of(context).textTheme.titleMedium?.copyWith(
                        fontWeight: FontWeight.w400,
                      ),
                  decoration: InputDecoration(
                    hintText: '123 456 789',
                    filled: false,
                    isDense: true,
                    contentPadding: const EdgeInsets.only(bottom: 10),
                    border: UnderlineInputBorder(
                      borderSide: BorderSide(color: c.border),
                    ),
                    enabledBorder: UnderlineInputBorder(
                      borderSide: BorderSide(color: c.border),
                    ),
                    focusedBorder: UnderlineInputBorder(
                      borderSide: BorderSide(color: c.primary),
                    ),
                  ),
                  onChanged: (value) {
                    ref
                        .read(onboardingControllerProvider.notifier)
                        .setMobileNumber(value);
                    setState(() {});
                  },
                ),
              ),
            ],
          ),
          const SizedBox(height: PayaboSpacing.xl),
          Text(
            "We'll send you a text message with a verification code. Message and data rates may apply.",
            style: Theme.of(context).textTheme.bodyLarge,
          ),
          const SizedBox(height: PayaboSpacing.xl),
          PayaboButton(
            label: 'Verify Number',
            onPressed: canVerify
                ? () => context.go('/auth/register/phone-code')
                : null,
          ),
        ],
      ),
    );
  }
}
