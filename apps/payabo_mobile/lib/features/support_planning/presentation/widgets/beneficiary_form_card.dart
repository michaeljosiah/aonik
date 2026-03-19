import 'package:flutter/material.dart';

import '../../../../shared/theme/payabo_color_resolver.dart';
import '../../../../shared/theme/payabo_radii.dart';
import '../../../../shared/theme/payabo_shadows.dart';
import '../../../../shared/theme/payabo_spacing.dart';

/// A card-style form for adding a beneficiary (person the user supports).
///
/// Collects name, relationship, location (optional), and phone number
/// (optional). Validates required fields before calling [onSubmit].
class BeneficiaryFormCard extends StatefulWidget {
  const BeneficiaryFormCard({
    super.key,
    required this.onSubmit,
    this.isLoading = false,
  });

  final void Function({
    required String name,
    required String relationship,
    String? location,
    String? phoneNumber,
  }) onSubmit;
  final bool isLoading;

  @override
  State<BeneficiaryFormCard> createState() => _BeneficiaryFormCardState();
}

class _BeneficiaryFormCardState extends State<BeneficiaryFormCard> {
  final _formKey = GlobalKey<FormState>();
  final _nameController = TextEditingController();
  final _relationshipController = TextEditingController();
  final _locationController = TextEditingController();
  final _phoneController = TextEditingController();

  @override
  void dispose() {
    _nameController.dispose();
    _relationshipController.dispose();
    _locationController.dispose();
    _phoneController.dispose();
    super.dispose();
  }

  void _submit() {
    if (!_formKey.currentState!.validate()) return;
    widget.onSubmit(
      name: _nameController.text.trim(),
      relationship: _relationshipController.text.trim(),
      location:
          _locationController.text.trim().isEmpty
              ? null
              : _locationController.text.trim(),
      phoneNumber:
          _phoneController.text.trim().isEmpty
              ? null
              : _phoneController.text.trim(),
    );
  }

  @override
  Widget build(BuildContext context) {
    final c = context.colors;

    return Container(
      width: double.infinity,
      decoration: BoxDecoration(
        color: c.surfaceBase,
        borderRadius: PayaboRadii.radiusSm,
        boxShadow: PayaboShadows.soft,
        border: Border.all(color: c.borderWarm),
      ),
      child: Padding(
        padding: const EdgeInsets.all(PayaboSpacing.xl),
        child: Form(
          key: _formKey,
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: <Widget>[
              Text(
                'Who do you support?',
                style: Theme.of(context).textTheme.titleMedium?.copyWith(
                      color: c.headerTitle,
                      fontWeight: FontWeight.w700,
                    ),
              ),
              const SizedBox(height: PayaboSpacing.xs),
              Text(
                'Add someone you regularly send money to or support financially.',
                style: Theme.of(context).textTheme.bodySmall?.copyWith(
                      color: c.textSecondary,
                    ),
              ),
              const SizedBox(height: PayaboSpacing.xl),

              _FormField(
                controller: _nameController,
                label: 'Name',
                hint: 'e.g. Mama Grace',
                validator: (value) {
                  if (value == null || value.trim().isEmpty) {
                    return 'Please enter their name';
                  }
                  return null;
                },
              ),
              const SizedBox(height: PayaboSpacing.lg),

              _FormField(
                controller: _relationshipController,
                label: 'Relationship',
                hint: 'e.g. Mother, Brother, Pastor',
                validator: (value) {
                  if (value == null || value.trim().isEmpty) {
                    return 'Please enter your relationship';
                  }
                  return null;
                },
              ),
              const SizedBox(height: PayaboSpacing.lg),

              _FormField(
                controller: _locationController,
                label: 'Location (optional)',
                hint: 'e.g. Lagos, Kumasi',
              ),
              const SizedBox(height: PayaboSpacing.lg),

              _FormField(
                controller: _phoneController,
                label: 'Phone number (optional)',
                hint: 'For mobile money payments',
                keyboardType: TextInputType.phone,
              ),
              const SizedBox(height: PayaboSpacing.xl),

              SizedBox(
                width: double.infinity,
                height: 48,
                child: FilledButton(
                  onPressed: widget.isLoading ? null : _submit,
                  style: FilledButton.styleFrom(
                    backgroundColor: c.primary,
                    foregroundColor: Colors.white,
                    shape: const RoundedRectangleBorder(
                      borderRadius: PayaboRadii.radiusSm,
                    ),
                  ),
                  child: widget.isLoading
                      ? const SizedBox(
                          width: 20,
                          height: 20,
                          child: CircularProgressIndicator(
                            strokeWidth: 2,
                            color: Colors.white,
                          ),
                        )
                      : const Text('Add person'),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _FormField extends StatelessWidget {
  const _FormField({
    required this.controller,
    required this.label,
    required this.hint,
    this.validator,
    this.keyboardType,
  });

  final TextEditingController controller;
  final String label;
  final String hint;
  final FormFieldValidator<String>? validator;
  final TextInputType? keyboardType;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        Text(
          label,
          style: Theme.of(context).textTheme.labelMedium?.copyWith(
                color: c.textSecondary,
                fontWeight: FontWeight.w600,
              ),
        ),
        const SizedBox(height: PayaboSpacing.xs),
        TextFormField(
          controller: controller,
          validator: validator,
          keyboardType: keyboardType,
          style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                color: c.textPrimary,
              ),
          decoration: InputDecoration(
            hintText: hint,
            hintStyle: Theme.of(context).textTheme.bodyMedium?.copyWith(
                  color: c.textSecondary.withValues(alpha: 0.5),
                ),
            filled: true,
            fillColor: c.surfaceWarm,
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
              borderSide: BorderSide(color: c.primary, width: 1.5),
            ),
            errorBorder: OutlineInputBorder(
              borderRadius: PayaboRadii.radiusSm,
              borderSide: BorderSide(color: c.danger),
            ),
          ),
        ),
      ],
    );
  }
}
