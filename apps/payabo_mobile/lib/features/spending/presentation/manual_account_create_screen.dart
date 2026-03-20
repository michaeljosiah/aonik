import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../data/repositories/account_links_repository.dart';
import '../../../data/repositories/repository_providers.dart';
import '../../../shared/reference/payabo_country_reference.dart';
import '../../../shared/theme/payabo_color_resolver.dart';
import '../../../shared/theme/payabo_radii.dart';
import '../../../shared/theme/payabo_spacing.dart';
import '../../../shared/widgets/payabo_button.dart';
import '../../../shared/widgets/payabo_screen_title_bar.dart';
import '../../../shared/widgets/payabo_warm_scaffold.dart';
import 'spending_accounts_state.dart';

const List<String> _accountTypes = <String>[
  'Current',
  'Savings',
  'Cash Wallet',
  'Credit Card',
  'Investment',
  'Other',
];

List<String> _supportedCurrencies() {
  final Set<String> seen = <String>{};
  final List<String> currencies = <String>[];
  for (final PayaboCountryReference country in payaboCountries) {
    if (seen.add(country.currencyCode)) {
      currencies.add(country.currencyCode);
    }
  }
  currencies.sort();
  return currencies;
}

String _currencySymbol(String code) {
  switch (code.toUpperCase()) {
    case 'GBP':
      return '\u00A3';
    case 'USD':
      return '\$';
    case 'EUR':
      return '\u20AC';
    case 'NGN':
      return '\u20A6';
    case 'KES':
      return 'KSh';
    case 'GHS':
      return 'GH\u20B5';
    case 'ZAR':
      return 'R';
    case 'CAD':
      return 'CA\$';
    case 'INR':
      return '\u20B9';
    case 'BWP':
      return 'P';
    case 'ZMW':
      return 'ZK';
    default:
      return code;
  }
}

class ManualAccountCreateScreen extends ConsumerStatefulWidget {
  const ManualAccountCreateScreen({super.key});

  @override
  ConsumerState<ManualAccountCreateScreen> createState() =>
      _ManualAccountCreateScreenState();
}

class _ManualAccountCreateScreenState
    extends ConsumerState<ManualAccountCreateScreen> {
  final GlobalKey<FormState> _formKey = GlobalKey<FormState>();
  final TextEditingController _nameController = TextEditingController();
  final TextEditingController _balanceController = TextEditingController();
  final TextEditingController _last4Controller = TextEditingController();

  String _selectedAccountType = _accountTypes.first;
  String _selectedCurrency = 'GBP';
  bool _isSubmitting = false;
  String? _errorMessage;

  @override
  void dispose() {
    _nameController.dispose();
    _balanceController.dispose();
    _last4Controller.dispose();
    super.dispose();
  }

  Future<void> _handleSubmit() async {
    if (!_formKey.currentState!.validate()) {
      return;
    }

    setState(() {
      _isSubmitting = true;
      _errorMessage = null;
    });

    try {
      final double? balance = _balanceController.text.trim().isNotEmpty
          ? double.tryParse(_balanceController.text.trim())
          : null;

      final String? last4 = _last4Controller.text.trim().isNotEmpty
          ? _last4Controller.text.trim()
          : null;

      final AccountLinksRepository repository =
          ref.read(accountLinksRepositoryProvider);

      final CreateManualAccountResult result =
          await repository.createManualAccount(
        CreateManualAccountRequest(
          name: _nameController.text.trim(),
          accountType: _selectedAccountType,
          currency: _selectedCurrency,
          startingBalance: balance,
          last4: last4,
        ),
      );

      ref.invalidate(accountLinksSummaryProvider);

      if (!mounted) {
        return;
      }

      ScaffoldMessenger.of(context)
        ..hideCurrentSnackBar()
        ..showSnackBar(
          SnackBar(
            content: Text(
              '${result.name} added as a manual account.',
            ),
          ),
        );

      context.go('/spending/accounts');
    } catch (error) {
      if (!mounted) {
        return;
      }

      setState(() {
        _isSubmitting = false;
        _errorMessage = error.toString();
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final c = context.colors;

    return PayaboWarmScaffold(
      body: Column(
        children: <Widget>[
          PayaboScreenTitleBar(
            title: 'Add manual account',
            onBack: () => context.go('/spending/accounts'),
          ),
          Expanded(
            child: ListView(
              padding: const EdgeInsets.symmetric(
                horizontal: PayaboSpacing.xl,
              ),
              children: <Widget>[
                _IntroCard(),
                const SizedBox(height: PayaboSpacing.xl),
                Form(
                  key: _formKey,
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: <Widget>[
                      _FormField(
                        label: 'Account name',
                        child: TextFormField(
                          controller: _nameController,
                          enabled: !_isSubmitting,
                          textInputAction: TextInputAction.next,
                          decoration: _inputDecoration(
                            context,
                            hintText: 'e.g. Travel cash wallet',
                          ),
                          style: _inputTextStyle(context),
                          validator: (String? value) {
                            if (value == null || value.trim().isEmpty) {
                              return 'Enter an account name';
                            }
                            return null;
                          },
                        ),
                      ),
                      const SizedBox(height: PayaboSpacing.lg),
                      _FormField(
                        label: 'Account type',
                        child: DropdownButtonFormField<String>(
                          value: _selectedAccountType,
                          decoration: _inputDecoration(context),
                          style: _inputTextStyle(context),
                          items: _accountTypes
                              .map(
                                (String type) => DropdownMenuItem<String>(
                                  value: type,
                                  child: Text(type),
                                ),
                              )
                              .toList(growable: false),
                          onChanged: _isSubmitting
                              ? null
                              : (String? value) {
                                  if (value != null) {
                                    setState(() {
                                      _selectedAccountType = value;
                                    });
                                  }
                                },
                        ),
                      ),
                      const SizedBox(height: PayaboSpacing.lg),
                      _FormField(
                        label: 'Currency',
                        child: DropdownButtonFormField<String>(
                          value: _selectedCurrency,
                          decoration: _inputDecoration(context),
                          style: _inputTextStyle(context),
                          items: _supportedCurrencies()
                              .map(
                                (String code) => DropdownMenuItem<String>(
                                  value: code,
                                  child: Text(
                                    '$code (${_currencySymbol(code)})',
                                  ),
                                ),
                              )
                              .toList(growable: false),
                          onChanged: _isSubmitting
                              ? null
                              : (String? value) {
                                  if (value != null) {
                                    setState(() {
                                      _selectedCurrency = value;
                                    });
                                  }
                                },
                        ),
                      ),
                      const SizedBox(height: PayaboSpacing.lg),
                      _FormField(
                        label: 'Starting balance (optional)',
                        child: TextFormField(
                          controller: _balanceController,
                          enabled: !_isSubmitting,
                          keyboardType: const TextInputType.numberWithOptions(
                            decimal: true,
                          ),
                          textInputAction: TextInputAction.next,
                          inputFormatters: <TextInputFormatter>[
                            FilteringTextInputFormatter.allow(
                              RegExp(r'^\d*\.?\d{0,2}'),
                            ),
                          ],
                          decoration: _inputDecoration(
                            context,
                            hintText: '0.00',
                            prefixText:
                                '${_currencySymbol(_selectedCurrency)} ',
                          ),
                          style: _inputTextStyle(context),
                          validator: (String? value) {
                            if (value == null || value.trim().isEmpty) {
                              return null;
                            }
                            final double? parsed = double.tryParse(value.trim());
                            if (parsed == null) {
                              return 'Enter a valid amount';
                            }
                            return null;
                          },
                        ),
                      ),
                      const SizedBox(height: PayaboSpacing.lg),
                      _FormField(
                        label: 'Last 4 digits (optional)',
                        child: TextFormField(
                          controller: _last4Controller,
                          enabled: !_isSubmitting,
                          keyboardType: TextInputType.number,
                          textInputAction: TextInputAction.done,
                          maxLength: 4,
                          inputFormatters: <TextInputFormatter>[
                            FilteringTextInputFormatter.digitsOnly,
                          ],
                          decoration: _inputDecoration(
                            context,
                            hintText: 'e.g. 4520',
                            counterText: '',
                          ),
                          style: _inputTextStyle(context),
                        ),
                      ),
                    ],
                  ),
                ),
                if (_errorMessage != null) ...<Widget>[
                  const SizedBox(height: PayaboSpacing.lg),
                  Container(
                    width: double.infinity,
                    decoration: BoxDecoration(
                      color: c.warning.withValues(alpha: 0.12),
                      borderRadius: BorderRadius.circular(PayaboRadii.lg),
                      border: Border.all(
                        color: c.warning.withValues(alpha: 0.3),
                      ),
                    ),
                    padding: const EdgeInsets.all(PayaboSpacing.md),
                    child: Text(
                      _errorMessage!,
                      style: Theme.of(context).textTheme.bodySmall?.copyWith(
                            color: c.accentBrown,
                            height: 1.4,
                          ),
                    ),
                  ),
                ],
                const SizedBox(height: PayaboSpacing.xl),
                if (_isSubmitting) ...<Widget>[
                  Row(
                    mainAxisAlignment: MainAxisAlignment.center,
                    children: <Widget>[
                      const SizedBox(
                        width: 18,
                        height: 18,
                        child: CircularProgressIndicator(strokeWidth: 2.2),
                      ),
                      const SizedBox(width: PayaboSpacing.sm),
                      Text(
                        'Creating account...',
                        style: Theme.of(context).textTheme.bodySmall?.copyWith(
                              color: c.accentBrownMuted,
                            ),
                      ),
                    ],
                  ),
                  const SizedBox(height: PayaboSpacing.lg),
                ],
                SizedBox(
                  width: double.infinity,
                  child: PayaboButton(
                    label: _isSubmitting ? 'Creating...' : 'Create account',
                    leading: _isSubmitting
                        ? null
                        : const Icon(Icons.add, size: 18),
                    onPressed: _isSubmitting ? null : _handleSubmit,
                  ),
                ),
                const SizedBox(height: PayaboSpacing.md),
                SizedBox(
                  width: double.infinity,
                  child: PayaboButton(
                    label: 'Cancel',
                    variant: PayaboButtonVariant.link,
                    onPressed: _isSubmitting
                        ? null
                        : () => context.go('/spending/accounts'),
                  ),
                ),
                const SizedBox(height: PayaboSpacing.x4),
              ],
            ),
          ),
        ],
      ),
    );
  }

  InputDecoration _inputDecoration(
    BuildContext context, {
    String? hintText,
    String? prefixText,
    String? counterText,
  }) {
    final c = context.colors;

    return InputDecoration(
      filled: true,
      fillColor: c.surfaceWarm,
      hintText: hintText,
      prefixText: prefixText,
      counterText: counterText,
      contentPadding: const EdgeInsets.symmetric(
        horizontal: PayaboSpacing.lg,
        vertical: PayaboSpacing.md,
      ),
      border: OutlineInputBorder(
        borderRadius: PayaboRadii.radiusSm,
        borderSide: BorderSide(color: c.borderWarm),
      ),
      enabledBorder: OutlineInputBorder(
        borderRadius: PayaboRadii.radiusSm,
        borderSide: BorderSide(color: c.borderWarm),
      ),
      focusedBorder: OutlineInputBorder(
        borderRadius: PayaboRadii.radiusSm,
        borderSide: BorderSide(color: c.primary, width: 1.4),
      ),
      errorBorder: OutlineInputBorder(
        borderRadius: PayaboRadii.radiusSm,
        borderSide: BorderSide(color: c.danger),
      ),
      focusedErrorBorder: OutlineInputBorder(
        borderRadius: PayaboRadii.radiusSm,
        borderSide: BorderSide(color: c.danger, width: 1.4),
      ),
      hintStyle: Theme.of(context).textTheme.bodyLarge?.copyWith(
            color: c.textSecondary.withValues(alpha: 0.5),
          ),
    );
  }

  TextStyle? _inputTextStyle(BuildContext context) {
    final c = context.colors;
    return Theme.of(context).textTheme.bodyLarge?.copyWith(
          color: c.ink,
        );
  }
}

class _IntroCard extends StatelessWidget {
  @override
  Widget build(BuildContext context) {
    final c = context.colors;

    return Container(
      decoration: BoxDecoration(
        color: c.spendingCardWarmElevated,
        borderRadius: PayaboRadii.radiusSm,
        border: Border.all(color: c.spendingQuickActionBorder),
      ),
      padding: const EdgeInsets.all(PayaboSpacing.xl),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Row(
            children: <Widget>[
              Container(
                width: 48,
                height: 48,
                decoration: BoxDecoration(
                  color: c.primary.withValues(alpha: 0.12),
                  borderRadius: BorderRadius.circular(18),
                ),
                child: Icon(
                  Icons.edit_note_outlined,
                  color: c.primary,
                  size: 24,
                ),
              ),
              const SizedBox(width: PayaboSpacing.md),
              Expanded(
                child: Text(
                  'Manual account',
                  style: Theme.of(context).textTheme.titleLarge?.copyWith(
                        color: c.accentBrown,
                        fontWeight: FontWeight.w700,
                      ),
                ),
              ),
            ],
          ),
          const SizedBox(height: PayaboSpacing.md),
          Text(
            'Track cash, off-platform balances, or accounts that cannot be linked to a bank connection. Manual accounts appear alongside linked accounts in Spend.',
            style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                  color: c.accentBrownMuted,
                  height: 1.45,
                ),
          ),
        ],
      ),
    );
  }
}

class _FormField extends StatelessWidget {
  const _FormField({
    required this.label,
    required this.child,
  });

  final String label;
  final Widget child;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        Text(
          label,
          style: Theme.of(context).textTheme.labelMedium?.copyWith(
                color: c.accentBrown,
                fontWeight: FontWeight.w700,
              ),
        ),
        const SizedBox(height: PayaboSpacing.sm),
        child,
      ],
    );
  }
}
