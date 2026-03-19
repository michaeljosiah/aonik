import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../data/repositories/catalog_repository.dart';
import '../../../shared/theme/payabo_borders.dart';
import '../../../shared/theme/payabo_colors.dart';
import '../../../shared/theme/payabo_spacing.dart';
import 'payment_flow_scaffold.dart';
import 'payment_flow_state.dart';

class PaymentCountryScreen extends ConsumerStatefulWidget {
  const PaymentCountryScreen({super.key});

  @override
  ConsumerState<PaymentCountryScreen> createState() =>
      _PaymentCountryScreenState();
}

class _PaymentCountryScreenState extends ConsumerState<PaymentCountryScreen> {
  final TextEditingController _searchController = TextEditingController();

  @override
  void dispose() {
    _searchController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final selectedCountryCode = ref.watch(
      paymentFlowControllerProvider.select((state) => state.countryCode),
    );
    final countriesValue = ref.watch(paymentCountriesProvider);

    return PaymentFlowScaffold(
      title: 'Select destination country',
      onBack: () => context.go('/pay'),
      onClose: () => context.go('/pay'),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          TextField(
            controller: _searchController,
            onChanged: (_) => setState(() {}),
            decoration: const InputDecoration(
              hintText: 'Search for a country',
              prefixIcon: Icon(Icons.search),
            ),
          ),
          const SizedBox(height: PayaboSpacing.lg),
          countriesValue.when(
            data: (List<CatalogCountry> countries) {
              final searchValue = _searchController.text.trim().toLowerCase();
              final filtered = countries
                  .where(
                    (country) =>
                        country.name.toLowerCase().contains(searchValue),
                  )
                  .toList(growable: false);

              return Column(
                children: filtered.map((country) {
                  final selected =
                      country.code.toUpperCase() == selectedCountryCode;

                  return InkWell(
                    onTap: () {
                      ref
                          .read(paymentFlowControllerProvider.notifier)
                          .setCountryCode(country.code);
                      context.go('/payments/providers');
                    },
                    child: Container(
                      padding: const EdgeInsets.symmetric(
                          vertical: PayaboSpacing.lg),
                      decoration: const BoxDecoration(
                        border: Border(bottom: PayaboBorders.defaultBorder),
                      ),
                      child: Row(
                        children: <Widget>[
                          CircleAvatar(
                            radius: 14,
                            backgroundColor: PayaboColors.background,
                            child: Text(
                              country.code,
                              style: Theme.of(context)
                                  .textTheme
                                  .bodySmall
                                  ?.copyWith(
                                    color: PayaboColors.ink,
                                    fontWeight: FontWeight.w700,
                                  ),
                            ),
                          ),
                          const SizedBox(width: PayaboSpacing.md),
                          Expanded(
                            child: Text(
                              country.name,
                              style: Theme.of(context).textTheme.bodyLarge,
                            ),
                          ),
                          if (selected)
                            const Icon(
                              Icons.check_circle,
                              color: PayaboColors.primary,
                            ),
                        ],
                      ),
                    ),
                  );
                }).toList(growable: false),
              );
            },
            loading: () => const Padding(
              padding: EdgeInsets.symmetric(vertical: PayaboSpacing.x2),
              child: Center(child: CircularProgressIndicator()),
            ),
            error: (error, stackTrace) {
              return Text('Unable to load countries: $error');
            },
          ),
        ],
      ),
    );
  }
}
