import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../data/api/api_exception.dart';
import '../../../shared/theme/payabo_colors.dart';
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

/// Common country entries used in the country code picker.
class _CountryEntry {
  const _CountryEntry({
    required this.flag,
    required this.dialCode,
    required this.name,
    required this.code,
  });

  final String flag;
  final String dialCode;
  final String name;
  final String code;
}

const List<_CountryEntry> _countries = <_CountryEntry>[
  _CountryEntry(flag: '\u{1F1EC}\u{1F1E7}', dialCode: '+44', name: 'United Kingdom', code: 'GB'),
  _CountryEntry(flag: '\u{1F1FA}\u{1F1F8}', dialCode: '+1', name: 'United States', code: 'US'),
  _CountryEntry(flag: '\u{1F1F3}\u{1F1EC}', dialCode: '+234', name: 'Nigeria', code: 'NG'),
  _CountryEntry(flag: '\u{1F1EE}\u{1F1EA}', dialCode: '+353', name: 'Ireland', code: 'IE'),
  _CountryEntry(flag: '\u{1F1EC}\u{1F1ED}', dialCode: '+233', name: 'Ghana', code: 'GH'),
  _CountryEntry(flag: '\u{1F1F0}\u{1F1EA}', dialCode: '+254', name: 'Kenya', code: 'KE'),
  _CountryEntry(flag: '\u{1F1FF}\u{1F1E6}', dialCode: '+27', name: 'South Africa', code: 'ZA'),
  _CountryEntry(flag: '\u{1F1EE}\u{1F1F3}', dialCode: '+91', name: 'India', code: 'IN'),
  _CountryEntry(flag: '\u{1F1E8}\u{1F1E6}', dialCode: '+1', name: 'Canada', code: 'CA'),
];

class EditContactScreen extends ConsumerStatefulWidget {
  const EditContactScreen({super.key});

  @override
  ConsumerState<EditContactScreen> createState() => _EditContactScreenState();
}

class _EditContactScreenState extends ConsumerState<EditContactScreen> {
  late final TextEditingController _phoneController;
  late _CountryEntry _selectedCountry;
  bool _saving = false;

  @override
  void initState() {
    super.initState();
    final state = ref.read(profileControllerProvider);
    _phoneController = TextEditingController(text: state.phone);
    _selectedCountry = _countries.firstWhere(
      (c) => c.code == state.countryCode,
      orElse: () => _countries.first,
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
    final phone = _phoneController.text.trim();
    if (phone.isEmpty) {
      _showError(context, 'Mobile number is required.');
      return;
    }

    setState(() {
      _saving = true;
    });

    try {
      await ref.read(profileControllerProvider.notifier).updatePhone(phone);

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
}

/// Inline country code prefix picker (flag + dial code + dropdown arrow).
class _CountryCodePicker extends StatelessWidget {
  const _CountryCodePicker({
    required this.selected,
    required this.onChanged,
  });

  final _CountryEntry selected;
  final ValueChanged<_CountryEntry> onChanged;

  @override
  Widget build(BuildContext context) {
    return InkWell(
      onTap: () => _showPicker(context),
      borderRadius: BorderRadius.circular(4),
      child: Container(
        padding: const EdgeInsets.symmetric(
          horizontal: PayaboSpacing.sm,
          vertical: PayaboSpacing.sm,
        ),
        decoration: const BoxDecoration(
          border: Border(
            bottom: BorderSide(color: PayaboColors.border, width: 1),
          ),
        ),
        child: Row(
          mainAxisSize: MainAxisSize.min,
          children: <Widget>[
            Text(selected.flag, style: const TextStyle(fontSize: 20)),
            const SizedBox(width: PayaboSpacing.xs),
            Text(
              selected.dialCode,
              style: Theme.of(context)
                  .textTheme
                  .bodyLarge
                  ?.copyWith(color: PayaboColors.ink),
            ),
            const SizedBox(width: 2),
            const Icon(Icons.arrow_drop_down,
                size: 20, color: PayaboColors.muted),
          ],
        ),
      ),
    );
  }

  Future<void> _showPicker(BuildContext context) async {
    final result = await showModalBottomSheet<_CountryEntry>(
      context: context,
      isScrollControlled: true,
      builder: (ctx) {
        return SafeArea(
          child: ListView.separated(
            shrinkWrap: true,
            physics: const ClampingScrollPhysics(),
            itemCount: _countries.length,
            separatorBuilder: (_, __) =>
                const Divider(height: 1, color: PayaboColors.border),
            itemBuilder: (ctx, index) {
              final entry = _countries[index];
              final isSelected = entry.code == selected.code;
              return ListTile(
                leading: Text(entry.flag, style: const TextStyle(fontSize: 22)),
                title: Text(entry.name),
                trailing: Text(
                  entry.dialCode,
                  style: TextStyle(
                    color: isSelected ? PayaboColors.primary : PayaboColors.muted,
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
