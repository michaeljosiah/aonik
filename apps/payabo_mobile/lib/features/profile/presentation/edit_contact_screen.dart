import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../data/api/api_exception.dart';
import '../../../shared/reference/payabo_country_reference.dart';
import '../../../shared/theme/payabo_color_resolver.dart';
import '../../../shared/theme/payabo_spacing.dart';
import '../../../shared/widgets/payabo_button.dart';
import '../../../shared/widgets/payabo_text_field.dart';
import 'profile_scaffold.dart';
import 'profile_state.dart';

void _showError(BuildContext context, String message) {
  ScaffoldMessenger.of(context)
    ..hideCurrentSnackBar()
    ..showSnackBar(SnackBar(content: Text(message)));
}

class EditContactScreen extends ConsumerStatefulWidget {
  const EditContactScreen({super.key});

  @override
  ConsumerState<EditContactScreen> createState() => _EditContactScreenState();
}

class _EditContactScreenState extends ConsumerState<EditContactScreen> {
  late final TextEditingController _phoneController;
  late PayaboCountryReference _selectedCountry;
  bool _saving = false;

  @override
  void initState() {
    super.initState();
    final state = ref.read(profileCoreProvider);
    _selectedCountry = resolvePayaboCountry(state.countryCode);
    _phoneController = TextEditingController(
      text: _stripDialCode(state.phone, _selectedCountry.dialCode),
    );
  }

  @override
  void dispose() {
    _phoneController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return ProfileScaffold(
      title: 'Contact number',
      backRoute: '/profile/personal-details',
      footer: PayaboButton(
        label: _saving ? 'Saving...' : 'Verify new number',
        onPressed: _saving ? null : _submit,
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          Row(
            crossAxisAlignment: CrossAxisAlignment.end,
            children: <Widget>[
              _CountryCodePicker(
                selected: _selectedCountry,
                onChanged: (entry) => setState(() => _selectedCountry = entry),
              ),
              const SizedBox(width: PayaboSpacing.sm),
              Expanded(
                child: PayaboTextField(
                  label: 'Mobile number',
                  variant: PayaboInputVariant.floating,
                  controller: _phoneController,
                  keyboardType: TextInputType.phone,
                  hintText: '123 456 789',
                ),
              ),
            ],
          ),
          const SizedBox(height: PayaboSpacing.md),
          Text(
            "We'll send you a text message with a verification code. Message and data rates may apply.",
            style: Theme.of(context).textTheme.bodySmall,
          ),
        ],
      ),
    );
  }

  Future<void> _submit() async {
    final localPhone = _phoneController.text.trim();
    final digits = localPhone.replaceAll(RegExp(r'\D'), '');
    if (digits.isEmpty) {
      _showError(context, 'Mobile number is required.');
      return;
    }

    setState(() {
      _saving = true;
    });

    try {
      await ref.read(profileCoreProvider.notifier).updatePhone(
            phone: '${_selectedCountry.dialCode}$digits',
            countryCode: _selectedCountry.code,
          );

      if (mounted) {
        context.go('/profile/personal-details');
      }
    } catch (error) {
      final message = error is ApiException
          ? error.message
          : 'Unable to update your phone number right now.';
      if (mounted) {
        _showError(context, message);
      }
      setState(() {
        _saving = false;
      });
    }
  }

  String _stripDialCode(String phone, String dialCode) {
    final String trimmedPhone = phone.trim();
    final String normalizedDialCode = dialCode.trim();
    if (trimmedPhone.startsWith(normalizedDialCode)) {
      return trimmedPhone.substring(normalizedDialCode.length);
    }

    return trimmedPhone;
  }
}

/// Inline country code prefix picker (flag + dial code + dropdown arrow).
class _CountryCodePicker extends StatelessWidget {
  const _CountryCodePicker({
    required this.selected,
    required this.onChanged,
  });

  final PayaboCountryReference selected;
  final ValueChanged<PayaboCountryReference> onChanged;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;

    return InkWell(
      onTap: () => _showPicker(context),
      borderRadius: BorderRadius.circular(4),
      child: Container(
        padding: const EdgeInsets.symmetric(
          horizontal: PayaboSpacing.sm,
          vertical: PayaboSpacing.sm,
        ),
        decoration: BoxDecoration(
          border: Border(
            bottom: BorderSide(color: c.border, width: 1),
          ),
        ),
        child: Row(
          mainAxisSize: MainAxisSize.min,
          children: <Widget>[
            Text(selected.flagEmoji, style: const TextStyle(fontSize: 20)),
            const SizedBox(width: PayaboSpacing.xs),
            Text(
              selected.dialCode,
              style: Theme.of(context)
                  .textTheme
                  .bodyLarge
                  ?.copyWith(color: c.ink),
            ),
            const SizedBox(width: 2),
            Icon(Icons.arrow_drop_down, size: 20, color: c.muted),
          ],
        ),
      ),
    );
  }

  Future<void> _showPicker(BuildContext context) async {
    final c = context.colors;

    final result = await showModalBottomSheet<PayaboCountryReference>(
      context: context,
      isScrollControlled: true,
      backgroundColor: c.surfaceBase,
      builder: (ctx) {
        final sheetColors = ctx.colors;

        return SafeArea(
          child: ListView.separated(
            shrinkWrap: true,
            physics: const ClampingScrollPhysics(),
            itemCount: payaboCountries.length,
            separatorBuilder: (_, __) =>
                Divider(height: 1, color: sheetColors.border),
            itemBuilder: (ctx, index) {
              final entry = payaboCountries[index];
              final isSelected = entry.code == selected.code;
              return ListTile(
                leading:
                    Text(entry.flagEmoji, style: const TextStyle(fontSize: 22)),
                title: Text(entry.name),
                trailing: Text(
                  entry.dialCode,
                  style: TextStyle(
                    color: isSelected ? sheetColors.primary : sheetColors.muted,
                    fontWeight: isSelected ? FontWeight.w700 : FontWeight.w400,
                  ),
                ),
                onTap: () => Navigator.of(ctx).pop(entry),
              );
            },
          ),
        );
      },
    );

    if (result != null) {
      onChanged(result);
    }
  }
}
