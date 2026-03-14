import 'package:flutter/material.dart';

import '../../../../shared/theme/payabo_color_resolver.dart';
import '../../../../shared/theme/payabo_radii.dart';
import '../../../../shared/theme/payabo_spacing.dart';
import '../../../../shared/widgets/payabo_button.dart';
import '../../domain/setup_enums.dart';
import '../../domain/setup_models.dart';
import 'setup_option_tile.dart';

/// Bottom anchored action card for the setup journey.
///
/// Displays the selectable options for the current step and
/// navigation controls (Next / Back). Slides up from the bottom
/// on initial appearance with a 500ms easeOutCubic animation.
class SetupActionCard extends StatefulWidget {
  const SetupActionCard({
    super.key,
    required this.stepConfig,
    required this.selectedIds,
    required this.onOptionTap,
    required this.onNext,
    this.onBack,
    this.onSecondaryAction,
    this.secondaryActionLabel,
    this.isFirstStep = false,
    this.nextLabel,
  });

  final SetupStepConfig stepConfig;
  final Set<String> selectedIds;
  final ValueChanged<String> onOptionTap;
  final VoidCallback onNext;
  final VoidCallback? onBack;
  final VoidCallback? onSecondaryAction;
  final String? secondaryActionLabel;
  final bool isFirstStep;
  final String? nextLabel;

  @override
  State<SetupActionCard> createState() => _SetupActionCardState();
}

class _SetupActionCardState extends State<SetupActionCard>
    with SingleTickerProviderStateMixin {
  late final AnimationController _slideController;
  late final Animation<Offset> _slideAnimation;
  late final Animation<double> _fadeAnimation;

  @override
  void initState() {
    super.initState();
    _slideController = AnimationController(
      vsync: this,
      duration: const Duration(milliseconds: 500),
    );
    _slideAnimation = Tween<Offset>(
      begin: const Offset(0, 0.3),
      end: Offset.zero,
    ).animate(CurvedAnimation(
      parent: _slideController,
      curve: Curves.easeOutCubic,
    ));
    _fadeAnimation = CurvedAnimation(
      parent: _slideController,
      curve: Curves.easeOut,
    );
    _slideController.forward();
  }

  @override
  void dispose() {
    _slideController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final c = context.colors;

    return SlideTransition(
      position: _slideAnimation,
      child: FadeTransition(
        opacity: _fadeAnimation,
        child: Container(
          decoration: BoxDecoration(
            color: c.surfaceBase,
            borderRadius: PayaboRadii.sheetTop,
            border: Border(
              top: BorderSide(color: c.borderWarm, width: 0.5),
            ),
            boxShadow: <BoxShadow>[
              BoxShadow(
                color: c.isDark
                    ? Colors.black26
                    : const Color(0x0D000000),
                blurRadius: 16,
                offset: const Offset(0, -4),
              ),
            ],
          ),
          child: SafeArea(
            top: false,
            child: Padding(
              padding: const EdgeInsets.fromLTRB(
                PayaboSpacing.xl,
                PayaboSpacing.xl,
                PayaboSpacing.xl,
                PayaboSpacing.lg,
              ),
              child: _buildContent(context),
            ),
          ),
        ),
      ),
    );
  }

  Widget _buildContent(BuildContext context) {
    final step = widget.stepConfig;

    if (step.type == SetupStepType.summary) {
      return _buildSummaryContent(context);
    }

    return Column(
      mainAxisSize: MainAxisSize.min,
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        // Options list — scrollable on small screens so the
        // navigation buttons remain visible and pinned at bottom.
        // singleAction steps show only a button, no option tiles.
        if (step.type != SetupStepType.singleAction)
          Flexible(
            child: AnimatedSwitcher(
              duration: const Duration(milliseconds: 300),
              switchInCurve: Curves.easeOutCubic,
              switchOutCurve: Curves.easeInCubic,
              child: SingleChildScrollView(
                key: ValueKey<String>(step.id),
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: <Widget>[
                    for (final option in step.options)
                      Padding(
                        padding:
                            const EdgeInsets.only(bottom: PayaboSpacing.sm),
                        child: SetupOptionTile(
                          label: option.label,
                          icon: option.icon,
                          isSelected:
                              widget.selectedIds.contains(option.id),
                          onTap: () => widget.onOptionTap(option.id),
                          showCheckIndicator: true,
                        ),
                      ),
                  ],
                ),
              ),
            ),
          ),

        const SizedBox(height: PayaboSpacing.lg),

        // Navigation buttons — always pinned at the bottom
        if (step.type == SetupStepType.singleAction)
          PayaboButton(
            label: step.options.first.label,
            onPressed: widget.onNext,
          )
        else
          Row(
            children: <Widget>[
              if (!widget.isFirstStep) ...<Widget>[
                Expanded(
                  child: PayaboButton(
                    label: 'Back',
                    variant: PayaboButtonVariant.secondary,
                    onPressed: widget.onBack,
                  ),
                ),
                const SizedBox(width: PayaboSpacing.md),
              ],
              Expanded(
                flex: widget.isFirstStep ? 1 : 2,
                child: PayaboButton(
                  label: widget.nextLabel ?? 'Continue',
                  onPressed: widget.selectedIds.isNotEmpty || step.canSkip
                      ? widget.onNext
                      : null,
                ),
              ),
            ],
          ),
      ],
    );
  }

  Widget _buildSummaryContent(BuildContext context) {
    return Column(
      mainAxisSize: MainAxisSize.min,
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        PayaboButton(
          label: widget.nextLabel ?? 'Let\'s go',
          onPressed: widget.onNext,
        ),
      ],
    );
  }
}
