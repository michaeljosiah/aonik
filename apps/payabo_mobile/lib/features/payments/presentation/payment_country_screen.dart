import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../data/repositories/catalog_repository.dart';
import '../../../shared/theme/payabo_color_resolver.dart';
import '../../../shared/theme/payabo_radii.dart';
import '../../../shared/theme/payabo_shadows.dart';
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
    final c = context.colors;
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
            decoration: InputDecoration(
              hintText: 'Search for a country',
              prefixIcon: Icon(Icons.search, color: c.textMuted),
              filled: true,
              fillColor: c.surfaceBase,
              border: OutlineInputBorder(
                borderRadius: BorderRadius.circular(20),
                borderSide: BorderSide(color: c.borderStrong),
              ),
              enabledBorder: OutlineInputBorder(
                borderRadius: BorderRadius.circular(20),
                borderSide: BorderSide(color: c.borderStrong),
              ),
              focusedBorder: OutlineInputBorder(
                borderRadius: BorderRadius.circular(20),
                borderSide: BorderSide(color: c.primary, width: 1.4),
              ),
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

              if (filtered.isEmpty) {
                return Padding(
                  padding:
                      const EdgeInsets.symmetric(vertical: PayaboSpacing.xl),
                  child: Text(
                    'No countries match your search yet.',
                    style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                          color: c.textSecondary,
                        ),
                  ),
                );
              }

              return Column(
                children: <Widget>[
                  for (var index = 0;
                      index < filtered.length;
                      index++) ...<Widget>[
                    _CountryOptionTile(
                      country: filtered[index],
                      selected: filtered[index].code.toUpperCase() ==
                          selectedCountryCode,
                      onTap: () {
                        ref
                            .read(paymentFlowControllerProvider.notifier)
                            .setCountryCode(filtered[index].code);
                        context.go('/payments/providers');
                      },
                    ),
                    if (index < filtered.length - 1)
                      const SizedBox(height: PayaboSpacing.md),
                  ],
                ],
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

class _CountryOptionTile extends StatelessWidget {
  const _CountryOptionTile({
    required this.country,
    required this.selected,
    required this.onTap,
  });

  final CatalogCountry country;
  final bool selected;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;

    return Material(
      color: Colors.transparent,
      child: InkWell(
        onTap: onTap,
        borderRadius: PayaboRadii.radiusSm,
        child: Ink(
          padding: const EdgeInsets.symmetric(
            horizontal: PayaboSpacing.lg,
            vertical: PayaboSpacing.lg,
          ),
          decoration: BoxDecoration(
            color: c.surfaceBase,
            borderRadius: PayaboRadii.radiusSm,
            border: Border.all(
              color: selected ? c.primary : c.borderStrong,
              width: selected ? 1.4 : 1,
            ),
            boxShadow: c.isDark ? PayaboShadows.soft : PayaboShadows.medium,
          ),
          child: Row(
            children: <Widget>[
              CircleAvatar(
                radius: 14,
                backgroundColor: c.surfaceWarmAccent,
                child: Text(
                  country.code,
                  style: Theme.of(context).textTheme.bodySmall?.copyWith(
                        color: c.textPrimary,
                        fontWeight: FontWeight.w700,
                      ),
                ),
              ),
              const SizedBox(width: PayaboSpacing.md),
              Expanded(
                child: Text(
                  country.name,
                  style: Theme.of(context).textTheme.bodyLarge?.copyWith(
                        color: c.textPrimary,
                      ),
                ),
              ),
              if (selected) Icon(Icons.check_circle, color: c.primary),
            ],
          ),
        ),
      ),
    );
  }
}
