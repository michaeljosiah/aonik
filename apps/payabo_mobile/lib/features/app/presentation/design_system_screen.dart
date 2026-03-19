import 'package:flutter/material.dart';

import '../../../shared/theme/payabo_spacing.dart';
import '../../../shared/widgets/payabo_bottom_nav.dart';
import '../../../shared/widgets/payabo_button.dart';
import '../../../shared/widgets/payabo_card.dart';
import '../../../shared/widgets/payabo_list_row.dart';
import '../../../shared/widgets/payabo_modal_sheet.dart';
import '../../../shared/widgets/payabo_otp_field.dart';
import '../../../shared/widgets/payabo_progress_bar.dart';
import '../../../shared/widgets/payabo_text_field.dart';

class DesignSystemScreen extends StatefulWidget {
  const DesignSystemScreen({super.key});

  @override
  State<DesignSystemScreen> createState() => _DesignSystemScreenState();
}

class _DesignSystemScreenState extends State<DesignSystemScreen> {
  final TextEditingController _emailController = TextEditingController();
  final TextEditingController _passwordController = TextEditingController();
  final TextEditingController _floatingController = TextEditingController();

  int _stepProgress = 1;
  int _smallProgress = 1;
  int _navIndex = 0;
  bool _otpEnabled = true;

  @override
  void dispose() {
    _emailController.dispose();
    _passwordController.dispose();
    _floatingController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Design System'),
      ),
      body: SafeArea(
        child: ListView(
          padding: PayaboSpacing.page,
          children: <Widget>[
            Text('Buttons', style: Theme.of(context).textTheme.titleLarge),
            const SizedBox(height: PayaboSpacing.sm),
            const PayaboButton(label: 'Primary'),
            const SizedBox(height: PayaboSpacing.sm),
            const PayaboButton(
                label: 'Secondary', variant: PayaboButtonVariant.secondary),
            const SizedBox(height: PayaboSpacing.sm),
            const PayaboButton(
                label: 'Link',
                variant: PayaboButtonVariant.link,
                expand: false),
            const SizedBox(height: PayaboSpacing.x2),
            Text('Form Inputs', style: Theme.of(context).textTheme.titleLarge),
            const SizedBox(height: PayaboSpacing.sm),
            PayaboTextField(
              label: 'Email',
              controller: _emailController,
              hintText: 'name@example.com',
              keyboardType: TextInputType.emailAddress,
              prefixIcon: const Icon(Icons.email_outlined),
            ),
            const SizedBox(height: PayaboSpacing.md),
            PayaboTextField(
              label: 'Password',
              controller: _passwordController,
              obscureText: true,
              suffixIcon: const Icon(Icons.visibility_outlined),
              errorText: 'Inline error sample',
            ),
            const SizedBox(height: PayaboSpacing.md),
            PayaboTextField(
              label: 'Floating label input',
              controller: _floatingController,
              variant: PayaboInputVariant.floating,
              hintText: 'Meter number',
            ),
            const SizedBox(height: PayaboSpacing.md),
            PayaboOtpField(
              enabled: _otpEnabled,
              onCompleted: (value) {
                ScaffoldMessenger.of(context).showSnackBar(
                  SnackBar(content: Text('OTP entered: $value')),
                );
              },
            ),
            const SizedBox(height: PayaboSpacing.sm),
            PayaboButton(
              label: _otpEnabled ? 'Disable OTP' : 'Enable OTP',
              variant: PayaboButtonVariant.secondary,
              size: PayaboButtonSize.sm,
              expand: false,
              onPressed: () {
                setState(() {
                  _otpEnabled = !_otpEnabled;
                });
              },
            ),
            const SizedBox(height: PayaboSpacing.x2),
            Text('Cards and Rows',
                style: Theme.of(context).textTheme.titleLarge),
            const SizedBox(height: PayaboSpacing.sm),
            const PayaboCard(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  Text('Card Title'),
                  SizedBox(height: PayaboSpacing.xs),
                  Text(
                      'Reusable card styles with tokenized border and elevation.'),
                ],
              ),
            ),
            const SizedBox(height: PayaboSpacing.md),
            PayaboListRow(
              title: 'ECG Power',
              subtitle: 'Electricity prepaid service',
              leading: const CircleAvatar(child: Icon(Icons.flash_on)),
              onTap: () {},
            ),
            const SizedBox(height: PayaboSpacing.x2),
            Text('Progress Components',
                style: Theme.of(context).textTheme.titleLarge),
            const SizedBox(height: PayaboSpacing.sm),
            PayaboStepProgressBar(
              steps: const <String>['COUNTRY', 'SERVICE', 'PAYMENT'],
              currentStep: _stepProgress,
            ),
            const SizedBox(height: PayaboSpacing.sm),
            Slider(
              value: _stepProgress.toDouble(),
              min: 0,
              max: 2,
              divisions: 2,
              label: 'Step $_stepProgress',
              onChanged: (value) {
                setState(() {
                  _stepProgress = value.toInt();
                });
              },
            ),
            const SizedBox(height: PayaboSpacing.md),
            PayaboSmallProgressBar(
              steps: const <String>['REQUEST', 'PENDING', 'DONE'],
              currentStep: _smallProgress,
            ),
            const SizedBox(height: PayaboSpacing.sm),
            Slider(
              value: _smallProgress.toDouble(),
              min: 0,
              max: 2,
              divisions: 2,
              label: 'Small $_smallProgress',
              onChanged: (value) {
                setState(() {
                  _smallProgress = value.toInt();
                });
              },
            ),
            const SizedBox(height: PayaboSpacing.x2),
            Text('Modal Pattern',
                style: Theme.of(context).textTheme.titleLarge),
            const SizedBox(height: PayaboSpacing.sm),
            PayaboButton(
              label: 'Open Bottom Sheet',
              onPressed: _showActionsSheet,
            ),
            const SizedBox(height: 120),
          ],
        ),
      ),
      bottomNavigationBar: PayaboBottomNav(
        items: const <PayaboBottomNavItem>[
          PayaboBottomNavItem(icon: Icons.home_outlined, label: 'Home'),
              PayaboBottomNavItem(
                icon: Icons.receipt_long_outlined, label: 'Pay'),
          PayaboBottomNavItem(icon: Icons.people_outline, label: 'Community'),
          PayaboBottomNavItem(icon: Icons.person_outline, label: 'Profile'),
        ],
        currentIndex: _navIndex,
        onTap: (index) {
          setState(() {
            _navIndex = index;
          });
        },
        onCenterTap: _showActionsSheet,
      ),
    );
  }

  Future<void> _showActionsSheet() async {
    await showPayaboModalSheet<void>(
      context: context,
      title: 'Quick Actions',
      child: Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          PayaboListRow(
            title: 'Add recipient',
            subtitle: 'Create a new recipient profile',
            leading: const Icon(Icons.person_add_alt_1_outlined),
            onTap: () => Navigator.of(context).pop(),
          ),
          const SizedBox(height: PayaboSpacing.sm),
          PayaboListRow(
            title: 'Send payment request',
            subtitle: 'Request help from friends and family',
            leading: const Icon(Icons.send_outlined),
            onTap: () => Navigator.of(context).pop(),
          ),
        ],
      ),
    );
  }
}
