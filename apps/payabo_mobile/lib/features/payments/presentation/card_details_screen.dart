import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../shared/theme/payabo_spacing.dart';
import '../../../shared/widgets/payabo_button.dart';
import '../../../shared/widgets/payabo_text_field.dart';
import 'payment_flow_scaffold.dart';
import 'payment_flow_state.dart';

class CardDetailsScreen extends ConsumerStatefulWidget {
  const CardDetailsScreen({super.key});

  @override
  ConsumerState<CardDetailsScreen> createState() => _CardDetailsScreenState();
}

class _CardDetailsScreenState extends ConsumerState<CardDetailsScreen> {
  final TextEditingController _cardNumberController = TextEditingController();
  final TextEditingController _expiryController = TextEditingController();
  final TextEditingController _cvcController = TextEditingController();

  @override
  void dispose() {
    _cardNumberController.dispose();
    _expiryController.dispose();
    _cvcController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final orderId = ref.watch(paymentOrderIdProvider);
    final saveCard = ref.watch(paymentSaveCardProvider);

    if (orderId.isEmpty) {
      return PaymentFlowScaffold(
        title: 'Enter your card details',
        onBack: () => context.go('/payments/service-details'),
        onClose: () => context.go('/dashboard'),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: <Widget>[
            const Text(
              'No draft order found. Please complete service details first.',
            ),
            const SizedBox(height: PayaboSpacing.lg),
            PayaboButton(
              label: 'Back to service details',
              onPressed: () => context.go('/payments/service-details'),
            ),
          ],
        ),
      );
    }

    final canCheckout = _cardNumberController.text.trim().length >= 12 &&
        _expiryController.text.trim().isNotEmpty &&
        _cvcController.text.trim().length >= 3;

    return PaymentFlowScaffold(
      title: 'Enter your card details',
      onBack: () => context.go('/payments/card-selection'),
      onClose: () => context.go('/dashboard'),
      footer: PayaboButton(
        label: 'Go to checkout',
        onPressed: canCheckout
            ? () {
                ref
                    .read(paymentFlowControllerProvider.notifier)
                    .selectCard('manual_card');
                context.go('/payments/checkout/card');
              }
            : null,
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          Text(
            'Card information is processed securely through our payment partner (Stripe).',
            style: Theme.of(context).textTheme.bodyMedium,
          ),
          const SizedBox(height: PayaboSpacing.lg),
          PayaboTextField(
            label: 'Card number',
            variant: PayaboInputVariant.floating,
            controller: _cardNumberController,
            keyboardType: TextInputType.number,
            hintText: 'Enter your card number',
            onChanged: (_) => setState(() {}),
          ),
          const SizedBox(height: PayaboSpacing.md),
          Row(
            children: <Widget>[
              Expanded(
                child: PayaboTextField(
                  label: 'Valid until',
                  variant: PayaboInputVariant.floating,
                  controller: _expiryController,
                  hintText: 'MM / YY',
                  onChanged: (_) => setState(() {}),
                ),
              ),
              const SizedBox(width: PayaboSpacing.md),
              Expanded(
                child: PayaboTextField(
                  label: 'Security code',
                  variant: PayaboInputVariant.floating,
                  controller: _cvcController,
                  keyboardType: TextInputType.number,
                  hintText: 'CVC',
                  onChanged: (_) => setState(() {}),
                ),
              ),
            ],
          ),
          const SizedBox(height: PayaboSpacing.md),
          SwitchListTile.adaptive(
            contentPadding: EdgeInsets.zero,
            value: saveCard,
            onChanged: (value) => ref
                .read(paymentFlowControllerProvider.notifier)
                .setSaveCard(value),
            title: const Text('Save card?'),
            subtitle: const Text(
              '(store your card details to use on future transactions)',
            ),
          ),
          const SizedBox(height: PayaboSpacing.xl),
          Center(
            child: Text(
              'Powered by Stripe',
              style: Theme.of(context).textTheme.bodySmall,
            ),
          ),
        ],
      ),
    );
  }
}
