import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../../../shared/theme/payabo_spacing.dart';
import '../../../shared/widgets/payabo_button.dart';
import 'payment_flow_scaffold.dart';

class PaymentReturnPlaceholderScreen extends StatelessWidget {
  const PaymentReturnPlaceholderScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return PaymentFlowScaffold(
      title: 'Payment return',
      onClose: () => context.go('/pay'),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          const Text(
            'Deep-link placeholder for partner callback and redirect handling.',
          ),
          const SizedBox(height: PayaboSpacing.lg),
          PayaboButton(
            label: 'Back to dashboard',
            onPressed: () => context.go('/pay'),
          ),
        ],
      ),
    );
  }
}
