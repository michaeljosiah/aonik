import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../data/repositories/repository_providers.dart';
import '../../../shared/theme/payabo_colors.dart';
import '../../../shared/theme/payabo_spacing.dart';
import '../../../shared/widgets/payabo_button.dart';
import '../../../shared/widgets/payabo_text_field.dart';
import 'payment_flow_scaffold.dart';
import 'payment_flow_state.dart';

const List<String> _serviceTypes = <String>[
  'Montage Cable TV',
  'Internet Data Bundle',
  'Electricity Prepaid',
];

const List<String> _recurringFrequencies = <String>[
  'Daily',
  'Weekly',
  'Monthly',
  'Quarterly',
];

class ServiceDetailsScreen extends ConsumerStatefulWidget {
  const ServiceDetailsScreen({super.key});

  @override
  ConsumerState<ServiceDetailsScreen> createState() =>
      _ServiceDetailsScreenState();
}

class _ServiceDetailsScreenState extends ConsumerState<ServiceDetailsScreen> {
  late final TextEditingController _smartCardController;
  late final TextEditingController _contactController;
  late final TextEditingController _amountController;

  String _serviceType = _serviceTypes.first;
  bool _isSubmitting = false;
  String? _validationMessage;

  @override
  void initState() {
    super.initState();
    final state = ref.read(paymentFlowControllerProvider);

    _serviceType =
        state.serviceType.isNotEmpty ? state.serviceType : _serviceTypes.first;
    _smartCardController = TextEditingController(text: state.smartCardId);
    _contactController = TextEditingController(text: state.contactReference);
    _amountController = TextEditingController(text: state.amount);
  }

  @override
  void dispose() {
    _smartCardController.dispose();
    _contactController.dispose();
    _amountController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final flowState = ref.watch(paymentFlowControllerProvider);
    final canContinue = _canSubmit(flowState);

    if (_smartCardController.text.isEmpty && flowState.smartCardId.isNotEmpty) {
      _smartCardController.text = flowState.smartCardId;
    }
    if (_contactController.text.isEmpty &&
        flowState.contactReference.isNotEmpty) {
      _contactController.text = flowState.contactReference;
    }
    if (_amountController.text.isEmpty && flowState.amount.isNotEmpty) {
      _amountController.text = flowState.amount;
    }
    if (_serviceType != flowState.serviceType &&
        flowState.serviceType.isNotEmpty) {
      _serviceType = flowState.serviceType;
    }

    return PaymentFlowScaffold(
      title: 'Service details',
      onBack: () => context.go('/payments/providers'),
      onClose: () => context.go('/dashboard'),
      footer: PayaboButton(
        label: _isSubmitting ? 'Processing...' : 'Pay now',
        onPressed: canContinue && !_isSubmitting ? _submitServiceDetails : null,
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          DropdownButtonFormField<String>(
            initialValue: _serviceType,
            decoration: const InputDecoration(
              labelText: 'Service type',
            ),
            items: _serviceTypes
                .map(
                  (type) => DropdownMenuItem<String>(
                    value: type,
                    child: Text(type),
                  ),
                )
                .toList(growable: false),
            onChanged: (value) {
              if (value == null) {
                return;
              }

              setState(() {
                _serviceType = value;
              });
              ref
                  .read(paymentFlowControllerProvider.notifier)
                  .setServiceType(value);
            },
          ),
          const SizedBox(height: PayaboSpacing.md),
          PayaboTextField(
            label: 'Smart Card ID',
            variant: PayaboInputVariant.floating,
            controller: _smartCardController,
            hintText: 'Enter the Smart Card ID number',
            onChanged: (value) {
              ref
                  .read(paymentFlowControllerProvider.notifier)
                  .setSmartCardId(value);
              setState(() {
                _validationMessage = null;
              });
            },
          ),
          const SizedBox(height: PayaboSpacing.md),
          PayaboTextField(
            label: 'Email or mobile number',
            variant: PayaboInputVariant.floating,
            controller: _contactController,
            hintText: 'Enter email or mobile number',
            onChanged: (value) {
              ref
                  .read(paymentFlowControllerProvider.notifier)
                  .setContactReference(value);
              setState(() {
                _validationMessage = null;
              });
            },
          ),
          const SizedBox(height: PayaboSpacing.md),
          PayaboTextField(
            label: 'Amount',
            variant: PayaboInputVariant.floating,
            controller: _amountController,
            keyboardType: const TextInputType.numberWithOptions(decimal: true),
            hintText: 'Enter amount',
            onChanged: (value) {
              ref.read(paymentFlowControllerProvider.notifier).setAmount(value);
              setState(() {
                _validationMessage = null;
              });
            },
          ),
          const SizedBox(height: PayaboSpacing.md),
          SwitchListTile.adaptive(
            contentPadding: EdgeInsets.zero,
            value: flowState.recurringBill,
            title: const Text('Recurring bill?'),
            subtitle:
                const Text('(not available for Request help with payment)'),
            onChanged: (value) {
              ref
                  .read(paymentFlowControllerProvider.notifier)
                  .setRecurringBill(value);
            },
          ),
          if (flowState.recurringBill) ...<Widget>[
            const SizedBox(height: PayaboSpacing.md),
            DropdownButtonFormField<String>(
              initialValue: flowState.recurringFrequency,
              decoration: const InputDecoration(labelText: 'Frequency'),
              items: _recurringFrequencies
                  .map(
                    (frequency) => DropdownMenuItem<String>(
                      value: frequency,
                      child: Text(frequency),
                    ),
                  )
                  .toList(growable: false),
              onChanged: (value) {
                if (value == null) {
                  return;
                }

                ref
                    .read(paymentFlowControllerProvider.notifier)
                    .setRecurringFrequency(value);
              },
            ),
            const SizedBox(height: PayaboSpacing.md),
            _DatePickerField(
              label: 'Starts',
              selectedDate: flowState.recurringStartsOn,
              onSelectDate: (date) => ref
                  .read(paymentFlowControllerProvider.notifier)
                  .setRecurringStartsOn(date),
            ),
            const SizedBox(height: PayaboSpacing.md),
            _DatePickerField(
              label: 'Ends',
              selectedDate: flowState.recurringEndsOn,
              onSelectDate: (date) => ref
                  .read(paymentFlowControllerProvider.notifier)
                  .setRecurringEndsOn(date),
            ),
            const SizedBox(height: PayaboSpacing.md),
            CheckboxListTile(
              contentPadding: EdgeInsets.zero,
              value: flowState.useSamePaymentMethodForRecurring,
              onChanged: (value) {
                ref
                    .read(paymentFlowControllerProvider.notifier)
                    .setUseSamePaymentMethodForRecurring(value ?? false);
              },
              title: const Text(
                'Use the same payment method for recurring bills.',
              ),
            ),
          ],
          if (_validationMessage != null) ...<Widget>[
            const SizedBox(height: PayaboSpacing.md),
            Text(
              _validationMessage!,
              style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                    color: PayaboColors.danger,
                  ),
            ),
          ],
        ],
      ),
    );
  }

  bool _canSubmit(PaymentFlowState state) {
    return state.providerId.isNotEmpty &&
        _smartCardController.text.trim().isNotEmpty &&
        _amountAsDouble(_amountController.text) > 0;
  }

  double _amountAsDouble(String value) {
    final sanitized = value.replaceAll(RegExp(r'[^0-9.]'), '');
    return double.tryParse(sanitized) ?? 0;
  }

  Future<void> _submitServiceDetails() async {
    final amount = _amountAsDouble(_amountController.text);
    if (amount <= 0) {
      setState(() {
        _validationMessage = 'Please enter a valid amount before continuing.';
      });
      return;
    }

    if (_smartCardController.text.trim().isEmpty) {
      setState(() {
        _validationMessage = 'Smart Card ID is required.';
      });
      return;
    }

    setState(() {
      _isSubmitting = true;
      _validationMessage = null;
    });

    final notifier = ref.read(paymentFlowControllerProvider.notifier);
    notifier.setServiceType(_serviceType);
    notifier.setSmartCardId(_smartCardController.text.trim());
    notifier.setContactReference(_contactController.text.trim());
    notifier.setAmount(_amountController.text.trim());

    try {
      await notifier.createDraftOrder(ref.read(orderRepositoryProvider));

      if (mounted) {
        context.go('/payments/payment-selection');
      }
    } catch (_) {
      if (!mounted) {
        return;
      }

      setState(() {
        _validationMessage =
            'Unable to save service details right now. Please try again.';
      });
    } finally {
      if (mounted) {
        setState(() {
          _isSubmitting = false;
        });
      }
    }
  }
}

class _DatePickerField extends StatelessWidget {
  const _DatePickerField({
    required this.label,
    required this.selectedDate,
    required this.onSelectDate,
  });

  final String label;
  final DateTime? selectedDate;
  final ValueChanged<DateTime?> onSelectDate;

  @override
  Widget build(BuildContext context) {
    final selectedLabel = selectedDate == null
        ? 'Select date'
        : '${selectedDate!.year}-${selectedDate!.month.toString().padLeft(2, '0')}-${selectedDate!.day.toString().padLeft(2, '0')}';

    return InkWell(
      onTap: () async {
        final now = DateTime.now();
        final selected = await showDatePicker(
          context: context,
          initialDate: selectedDate ?? now,
          firstDate: DateTime(now.year - 2),
          lastDate: DateTime(now.year + 8),
        );

        onSelectDate(selected);
      },
      child: InputDecorator(
        decoration: InputDecoration(labelText: label),
        child: Text(selectedLabel),
      ),
    );
  }
}
