import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../data/repositories/payment_repository.dart';
import '../../../data/repositories/repository_providers.dart';
import '../../../shared/theme/payabo_color_resolver.dart';
import '../../../shared/theme/payabo_spacing.dart';
import '../../../shared/widgets/payabo_button.dart';
import '../../../shared/widgets/payabo_card.dart';
import 'payment_flow_scaffold.dart';
import 'payment_flow_state.dart';

class ThankYouScreen extends ConsumerStatefulWidget {
  const ThankYouScreen({super.key});

  @override
  ConsumerState<ThankYouScreen> createState() => _ThankYouScreenState();
}

class _ThankYouScreenState extends ConsumerState<ThankYouScreen> {
  bool _refreshing = false;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    final statusState = ref.watch(paymentStatusSummaryProvider);
    final status = statusState.paymentResult ?? PaymentResult.pending;
    final isFirstPending =
        status == PaymentResult.pending && statusState.statusChecks == 0;
    final isSecondPending =
        status == PaymentResult.pending && statusState.statusChecks > 0;

    final icon = status == PaymentResult.success
        ? Icons.check_circle_outline
        : status == PaymentResult.failed
            ? Icons.error_outline
            : Icons.hourglass_top;

    final color = status == PaymentResult.success
        ? c.success
        : status == PaymentResult.failed
            ? c.danger
            : c.muted;

    final title = status == PaymentResult.success
        ? 'Your bill is paid'
        : status == PaymentResult.failed
            ? 'Payment failed'
            : 'Awaiting payment';

    return PaymentFlowScaffold(
      title: 'Thank you for your order',
      onClose: () => context.go('/pay'),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          const SizedBox(height: PayaboSpacing.md),
          Icon(icon, size: 88, color: color),
          const SizedBox(height: PayaboSpacing.md),
          Text(
            'Transaction status:\n$title',
            textAlign: TextAlign.center,
            style: Theme.of(context).textTheme.titleLarge?.copyWith(
                  color: color,
                  fontWeight: FontWeight.w700,
                ),
          ),
          const SizedBox(height: PayaboSpacing.sm),
          Text(
            'Order: ${statusState.orderId.isEmpty ? '-' : statusState.orderId}',
            textAlign: TextAlign.center,
            style: Theme.of(context).textTheme.bodySmall,
          ),
          const SizedBox(height: PayaboSpacing.lg),
          _ProgressSteps(
            activeIndex: status == PaymentResult.success
                ? 2
                : isSecondPending
                    ? 1
                    : 0,
          ),
          const SizedBox(height: PayaboSpacing.lg),
          if (status == PaymentResult.success)
            _PointsCard(orderId: statusState.orderId),
          const SizedBox(height: PayaboSpacing.lg),
          if (status != PaymentResult.success)
            PayaboButton(
              label: _refreshing ? 'Refreshing...' : 'Refresh status',
              onPressed: _refreshing ? null : _refreshStatus,
            ),
          if (status == PaymentResult.failed) ...<Widget>[
            const SizedBox(height: PayaboSpacing.md),
            PayaboButton(
              label: 'Retry payment',
              variant: PayaboButtonVariant.secondary,
              onPressed: () {
                ref
                    .read(paymentFlowControllerProvider.notifier)
                    .resetForNewCheckout();
                context.go('/payments/payment-selection');
              },
            ),
          ],
          const SizedBox(height: PayaboSpacing.md),
          PayaboButton(
            label: status == PaymentResult.success
                ? 'Back to dashboard'
                : 'Continue',
            variant: PayaboButtonVariant.secondary,
            onPressed: () => context.go('/pay'),
          ),
          const SizedBox(height: PayaboSpacing.md),
          PayaboCard(
            child: InkWell(
              onTap: () {
                ScaffoldMessenger.of(context)
                  ..hideCurrentSnackBar()
                  ..showSnackBar(
                    const SnackBar(content: Text('Help centre coming soon.')),
                  );
              },
              child: const Text('Help'),
            ),
          ),
          if (status == PaymentResult.success) ...<Widget>[
            const SizedBox(height: PayaboSpacing.sm),
            PayaboCard(
              child: InkWell(
                onTap: () {
                  ScaffoldMessenger.of(context)
                    ..hideCurrentSnackBar()
                    ..showSnackBar(
                      const SnackBar(
                        content: Text('Receipt download coming soon.'),
                      ),
                    );
                },
                child: const Text('Download receipt'),
              ),
            ),
            const SizedBox(height: PayaboSpacing.sm),
            PayaboCard(
              child: InkWell(
                onTap: () {
                  ScaffoldMessenger.of(context)
                    ..hideCurrentSnackBar()
                    ..showSnackBar(
                      const SnackBar(
                        content: Text('Send receipt coming soon.'),
                      ),
                    );
                },
                child: const Text('Send receipt'),
              ),
            ),
          ],
          if (isFirstPending) ...<Widget>[
            const SizedBox(height: PayaboSpacing.sm),
            Text(
              'Status 1/3: Order received',
              textAlign: TextAlign.center,
              style: Theme.of(context).textTheme.bodySmall,
            ),
          ],
        ],
      ),
    );
  }

  Future<void> _refreshStatus() async {
    setState(() {
      _refreshing = true;
    });

    try {
      await ref
          .read(paymentFlowControllerProvider.notifier)
          .refreshPaymentStatus(ref.read(paymentRepositoryProvider));
    } catch (_) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('Could not refresh payment status.')),
        );
      }
    } finally {
      if (mounted) {
        setState(() {
          _refreshing = false;
        });
      }
    }
  }
}

class _PointsCard extends ConsumerWidget {
  const _PointsCard({required this.orderId});

  final String orderId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final c = context.colors;
    final pointsAsync = ref.watch(paymentPointsSummaryProvider);
    final points = pointsAsync.value;

    if (points == null) {
      return const SizedBox.shrink();
    }

    return PayaboCard(
      child: Column(
        children: <Widget>[
          Text('${points.pointsLabel} points earned in this transaction:'),
          const SizedBox(height: PayaboSpacing.sm),
          Text(
            '${points.pointsEarned}',
            style: TextStyle(
              fontSize: 42,
              color: c.success,
              fontWeight: FontWeight.w700,
            ),
          ),
          const SizedBox(height: 4),
          Text('TOTAL POINTS ${points.totalPoints}'),
        ],
      ),
    );
  }
}

class _ProgressSteps extends StatelessWidget {
  const _ProgressSteps({required this.activeIndex});

  final int activeIndex;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    const labels = <String>['Order\nreceived', 'Payment\nsent', 'Bill\npaid'];

    return Row(
      children: List<Widget>.generate(labels.length, (index) {
        final isActive = index <= activeIndex;
        return Expanded(
          child: Column(
            children: <Widget>[
              Row(
                children: <Widget>[
                  Expanded(
                    child: Container(
                      height: 4,
                      color: isActive ? c.primary : c.border,
                    ),
                  ),
                  if (index < labels.length - 1) const SizedBox(width: 6),
                ],
              ),
              const SizedBox(height: 8),
                Text(
                  labels[index],
                  textAlign: TextAlign.center,
                  style: Theme.of(context).textTheme.bodySmall?.copyWith(
                        color: isActive ? c.ink : c.muted,
                        fontWeight: isActive ? FontWeight.w700 : FontWeight.w400,
                      ),
                ),
            ],
          ),
        );
      }),
    );
  }
}
