import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../shared/theme/payabo_spacing.dart';
import '../../../shared/widgets/payabo_button.dart';
import '../../../shared/widgets/payabo_text_field.dart';
import 'profile_scaffold.dart';
import 'profile_state.dart';

class MarketingEmailScreen extends ConsumerStatefulWidget {
  const MarketingEmailScreen({super.key});

  @override
  ConsumerState<MarketingEmailScreen> createState() =>
      _MarketingEmailScreenState();
}

class _MarketingEmailScreenState extends ConsumerState<MarketingEmailScreen> {
  late final TextEditingController _emailController;

  @override
  void initState() {
    super.initState();
    _emailController = TextEditingController(
        text: ref.read(profileControllerProvider).marketingEmail);
  }

  @override
  void dispose() {
    _emailController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return ProfileScaffold(
      title: 'Email for marketing',
      backRoute: '/profile/marketing',
      footer: PayaboButton(
        label: 'Save changes',
        onPressed: () {
          ref
              .read(profileControllerProvider.notifier)
              .setMarketingEmail(_emailController.text);
          context.go('/profile/marketing');
        },
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          Text(
            'You can set a different email to receive our marketing communications, this will not affect your login details.',
            style: Theme.of(context).textTheme.bodySmall,
          ),
          const SizedBox(height: PayaboSpacing.md),
          PayaboTextField(
            label: 'Email address for marketing',
            variant: PayaboInputVariant.floating,
            controller: _emailController,
            keyboardType: TextInputType.emailAddress,
          ),
        ],
      ),
    );
  }
}
