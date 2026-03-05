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

class EditNameScreen extends ConsumerStatefulWidget {
  const EditNameScreen({super.key});

  @override
  ConsumerState<EditNameScreen> createState() => _EditNameScreenState();
}

class _EditNameScreenState extends ConsumerState<EditNameScreen> {
  late final TextEditingController _firstNameController;
  late final TextEditingController _lastNameController;
  String? _error;
  bool _saving = false;

  @override
  void initState() {
    super.initState();
    final state = ref.read(profileControllerProvider);
    _firstNameController = TextEditingController(text: state.firstName);
    _lastNameController = TextEditingController(text: state.lastName);
  }

  @override
  void dispose() {
    _firstNameController.dispose();
    _lastNameController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return ProfileScaffold(
      title: 'Name',
      backRoute: '/profile/personal-details',
      footer: PayaboButton(
        label: _saving ? 'Saving...' : 'Save changes',
        onPressed: _saving ? null : _submit,
      ),
      child: Column(
        children: <Widget>[
          PayaboTextField(
            label: 'First name',
            variant: PayaboInputVariant.floating,
            controller: _firstNameController,
            hintText: 'First name',
          ),
          const SizedBox(height: PayaboSpacing.md),
          PayaboTextField(
            label: 'Last name',
            variant: PayaboInputVariant.floating,
            controller: _lastNameController,
            hintText: 'Last name',
          ),
          if (_error != null) ...<Widget>[
            const SizedBox(height: PayaboSpacing.sm),
            Text(
              _error!,
              style: Theme.of(context)
                  .textTheme
                  .bodySmall
                  ?.copyWith(color: PayaboColors.danger),
            ),
          ],
        ],
      ),
    );
  }

  Future<void> _submit() async {
    final firstName = _firstNameController.text.trim();
    final lastName = _lastNameController.text.trim();
    if (firstName.isEmpty || lastName.isEmpty) {
      setState(() {
        _error = 'Both first and last name are required.';
      });
      return;
    }

    setState(() {
      _saving = true;
      _error = null;
    });

    try {
      await ref.read(profileControllerProvider.notifier).updateName(
            firstName: firstName,
            lastName: lastName,
          );

      if (mounted) {
        context.go('/profile/personal-details');
      }
    } catch (error) {
      setState(() {
        _error = error is ApiException
            ? error.message
            : 'Unable to update your name right now.';
        _saving = false;
      });
    }
  }
}
