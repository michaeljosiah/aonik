import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../shared/theme/payabo_color_resolver.dart';
import '../../../shared/theme/payabo_spacing.dart';
import '../application/support_planning_controller.dart';
import 'widgets/beneficiary_form_card.dart';

/// Screen for adding a new beneficiary (person the user supports).
///
/// Accessible from the Simi dashboard nudge or from a future
/// support planning hub.
class AddBeneficiaryScreen extends ConsumerStatefulWidget {
  const AddBeneficiaryScreen({super.key});

  @override
  ConsumerState<AddBeneficiaryScreen> createState() =>
      _AddBeneficiaryScreenState();
}

class _AddBeneficiaryScreenState extends ConsumerState<AddBeneficiaryScreen> {
  @override
  void initState() {
    super.initState();
    // Load existing data so the controller state is populated.
    Future.microtask(
      () => ref.read(supportPlanningControllerProvider.notifier).loadAll(),
    );
  }

  @override
  Widget build(BuildContext context) {
    final state = ref.watch(supportPlanningControllerProvider);
    final c = context.colors;

    return Scaffold(
      backgroundColor: c.surfaceWarm,
      appBar: AppBar(
        backgroundColor: Colors.transparent,
        elevation: 0,
        leading: IconButton(
          icon: Icon(Icons.arrow_back_rounded, color: c.headerTitle),
          onPressed: () => context.pop(),
        ),
        title: Text(
          'Add someone you support',
          style: Theme.of(context).textTheme.titleMedium?.copyWith(
                color: c.headerTitle,
                fontWeight: FontWeight.w700,
              ),
        ),
        centerTitle: false,
      ),
      body: SingleChildScrollView(
        padding: const EdgeInsets.all(PayaboSpacing.xl),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: <Widget>[
            // Simi context message
            Container(
              width: double.infinity,
              padding: const EdgeInsets.all(PayaboSpacing.lg),
              decoration: BoxDecoration(
                color: c.primary.withValues(alpha: 0.06),
                borderRadius: BorderRadius.circular(12),
                border: Border.all(
                  color: c.primary.withValues(alpha: 0.12),
                ),
              ),
              child: Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  Icon(
                    Icons.auto_awesome_rounded,
                    size: 18,
                    color: c.primary,
                  ),
                  const SizedBox(width: PayaboSpacing.md),
                  Expanded(
                    child: Text(
                      'I noticed you support family members. Adding them '
                      'here helps me plan around your commitments and remind '
                      'you before due dates.',
                      style: Theme.of(context).textTheme.bodySmall?.copyWith(
                            color: c.textSecondary,
                            height: 1.4,
                          ),
                    ),
                  ),
                ],
              ),
            ),
            const SizedBox(height: PayaboSpacing.xl),

            BeneficiaryFormCard(
              isLoading: state.isLoading,
              onSubmit: ({
                required String name,
                required String relationship,
                String? location,
                String? phoneNumber,
              }) async {
                final controller = ref.read(
                  supportPlanningControllerProvider.notifier,
                );
                final result = await controller.addBeneficiary(
                  name: name,
                  relationship: relationship,
                  location: location,
                  phoneNumber: phoneNumber,
                );
                if (result != null && context.mounted) {
                  ScaffoldMessenger.of(context).showSnackBar(
                    SnackBar(
                      content: Text('${result.name} added successfully'),
                    ),
                  );
                  context.pop();
                }
              },
            ),

            if (state.error != null) ...[
              const SizedBox(height: PayaboSpacing.lg),
              Text(
                state.error!,
                style: Theme.of(context).textTheme.bodySmall?.copyWith(
                      color: c.danger,
                    ),
              ),
            ],
          ],
        ),
      ),
    );
  }
}
