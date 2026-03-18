import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../data/repositories/catalog_repository.dart';
import '../../../shared/theme/payabo_color_resolver.dart';
import '../../../shared/theme/payabo_radii.dart';
import '../../../shared/theme/payabo_spacing.dart';
import '../../../shared/widgets/payabo_card.dart';
import 'payment_flow_scaffold.dart';
import 'payment_flow_state.dart';

class ProviderListScreen extends ConsumerStatefulWidget {
  const ProviderListScreen({super.key});

  @override
  ConsumerState<ProviderListScreen> createState() => _ProviderListScreenState();
}

class _ProviderListScreenState extends ConsumerState<ProviderListScreen> {
  final TextEditingController _searchController = TextEditingController();

  @override
  void dispose() {
    _searchController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    final selectedCategory = ref.watch(paymentCategoryProvider);
    final providersValue = ref.watch(paymentProvidersProvider);
    final providerCategories =
        ref.watch(paymentProviderCategoriesProvider).value ?? const <String>[];

    return PaymentFlowScaffold(
      title: 'Select the service provider',
      onBack: () => context.go('/payments/country'),
      onClose: () => context.go('/dashboard'),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          TextField(
            controller: _searchController,
            onChanged: (_) => setState(() {}),
            decoration: const InputDecoration(
              hintText: 'Search for a provider',
              prefixIcon: Icon(Icons.search),
            ),
          ),
          const SizedBox(height: PayaboSpacing.md),
          SizedBox(
            height: 44,
            child: ListView.separated(
              scrollDirection: Axis.horizontal,
              itemCount: providerCategories.length,
              separatorBuilder: (_, __) =>
                  const SizedBox(width: PayaboSpacing.sm),
              itemBuilder: (BuildContext context, int index) {
                final category = providerCategories[index];
                final selected = selectedCategory == category;

                return ChoiceChip(
                  label: Text(category),
                  selected: selected,
                  selectedColor: c.primary,
                  backgroundColor: c.surfaceBase,
                  labelStyle: Theme.of(context).textTheme.bodyMedium?.copyWith(
                        color: selected ? Colors.white : c.ink,
                        fontWeight:
                            selected ? FontWeight.w700 : FontWeight.w400,
                      ),
                  onSelected: (_) => ref
                      .read(paymentFlowControllerProvider.notifier)
                      .setCategory(category),
                  shape: RoundedRectangleBorder(
                    borderRadius: BorderRadius.circular(PayaboRadii.pill),
                    side: BorderSide(color: c.border),
                  ),
                );
              },
            ),
          ),
          const SizedBox(height: PayaboSpacing.lg),
          providersValue.when(
            data: (List<CatalogProvider> providers) {
              final filtered = providers
                  .where(
                    (provider) =>
                        _providerMatchesCategory(provider, selectedCategory),
                  )
                  .where(
                    (provider) => provider.name.toLowerCase().contains(
                          _searchController.text.trim().toLowerCase(),
                        ),
                  )
                  .toList(growable: false);

              if (filtered.isEmpty) {
                return const _ProviderPlaceholderList();
              }

              return Column(
                children: filtered.map((provider) {
                  return Padding(
                    padding: const EdgeInsets.only(bottom: PayaboSpacing.md),
                    child: InkWell(
                      onTap: () {
                        ref
                            .read(paymentFlowControllerProvider.notifier)
                            .setProvider(
                              providerId: provider.id,
                              providerName: provider.name,
                            );
                        context.go('/payments/service-details');
                      },
                      borderRadius: BorderRadius.circular(PayaboRadii.sm),
                      child: PayaboCard(
                        padding: EdgeInsets.zero,
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: <Widget>[
                            Container(
                              height: 120,
                              decoration: const BoxDecoration(
                                borderRadius: BorderRadius.only(
                                  topLeft: Radius.circular(PayaboRadii.sm),
                                  topRight: Radius.circular(PayaboRadii.sm),
                                ),
                                gradient: LinearGradient(
                                  colors: <Color>[
                                    Color(0xFF1750A5),
                                    Color(0xFF39A7EA)
                                  ],
                                  begin: Alignment.topLeft,
                                  end: Alignment.bottomRight,
                                ),
                              ),
                              child: Align(
                                alignment: Alignment.topLeft,
                                child: Padding(
                                  padding:
                                      const EdgeInsets.all(PayaboSpacing.md),
                                  child: provider.id == filtered.first.id
                                      ? Container(
                                          padding: const EdgeInsets.symmetric(
                                            horizontal: PayaboSpacing.sm,
                                            vertical: 3,
                                          ),
                                          decoration: BoxDecoration(
                                            color: c.primary,
                                            borderRadius:
                                                BorderRadius.circular(4),
                                          ),
                                          child: Text(
                                            'SPONSORED',
                                            style: Theme.of(context)
                                                .textTheme
                                                 .bodySmall
                                                 ?.copyWith(
                                                   color: Colors.white,
                                                   fontWeight: FontWeight.w700,
                                                   fontSize: 10,
                                                 ),
                                          ),
                                        )
                                      : const SizedBox.shrink(),
                                ),
                              ),
                            ),
                            Padding(
                              padding: const EdgeInsets.all(PayaboSpacing.lg),
                              child: Column(
                                crossAxisAlignment: CrossAxisAlignment.start,
                                children: <Widget>[
                                  Text(
                                    provider.name,
                                    style:
                                        Theme.of(context).textTheme.titleSmall,
                                  ),
                                  const SizedBox(height: 4),
                                  Text(
                                    'Lorem ipsum dolor sit amet, conse tetur adipiscing elit. Nullam tincidunt...',
                                    style:
                                        Theme.of(context).textTheme.bodySmall,
                                  ),
                                ],
                              ),
                            ),
                          ],
                        ),
                      ),
                    ),
                  );
                }).toList(growable: false),
              );
            },
            loading: () => const Center(
              child: Padding(
                padding: EdgeInsets.all(PayaboSpacing.xl),
                child: CircularProgressIndicator(),
              ),
            ),
            error: (error, stackTrace) {
              return Text('Unable to load providers: $error');
            },
          ),
        ],
      ),
    );
  }

  bool _providerMatchesCategory(CatalogProvider provider, String category) {
    if (category == 'All') {
      return true;
    }

    final name = provider.name.toLowerCase();

    switch (category) {
      case 'Electricity':
        return name.contains('power') || name.contains('electric');
      case 'TV providers':
        return name.contains('tv') || name.contains('montage');
      case 'Education':
        return name.contains('school') || name.contains('education');
      case 'Hospital':
        return name.contains('hospital') || name.contains('health');
      case 'Others':
        return !(name.contains('power') ||
            name.contains('electric') ||
            name.contains('tv') ||
            name.contains('school') ||
            name.contains('hospital'));
      default:
        return true;
    }
  }
}

class _ProviderPlaceholderList extends StatelessWidget {
  const _ProviderPlaceholderList();

  @override
  Widget build(BuildContext context) {
    final c = context.colors;

    return Column(
      children: List<Widget>.generate(3, (int index) {
        return Padding(
          padding: const EdgeInsets.only(bottom: PayaboSpacing.md),
          child: PayaboCard(
            padding: EdgeInsets.zero,
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                Container(
                  height: 120,
                  decoration: BoxDecoration(
                    borderRadius: const BorderRadius.only(
                      topLeft: Radius.circular(PayaboRadii.sm),
                      topRight: Radius.circular(PayaboRadii.sm),
                    ),
                    color: c.background,
                  ),
                ),
                Padding(
                  padding: const EdgeInsets.all(PayaboSpacing.lg),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: <Widget>[
                      Text(
                        'Service provider name',
                        style: Theme.of(context).textTheme.titleSmall,
                      ),
                      const SizedBox(height: 4),
                      Text(
                        'Lorem ipsum dolor sit amet, conse tetur adipiscing elit. Nullam tincidunt...',
                        style: Theme.of(context).textTheme.bodySmall,
                      ),
                    ],
                  ),
                ),
              ],
            ),
          ),
        );
      }),
    );
  }
}
