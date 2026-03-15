import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_svg/flutter_svg.dart';
import 'package:go_router/go_router.dart';

import '../../../app/auth/auth_controller.dart';
import '../../../app/demo/demo_mode.dart';
import '../../../data/api/api_exception.dart';
import '../../../shared/theme/payabo_color_resolver.dart';
import '../../../shared/theme/payabo_radii.dart';
import '../../../shared/theme/payabo_spacing.dart';
import '../../../shared/widgets/payabo_button.dart';
import '../../../shared/widgets/payabo_text_field.dart';
import '../../setup_journey/application/setup_journey_controller.dart';
import 'auth_flow_scaffold.dart';
import 'onboarding_flow_state.dart';

class LoginScreen extends ConsumerStatefulWidget {
  const LoginScreen({super.key});

  @override
  ConsumerState<LoginScreen> createState() => _LoginScreenState();
}

class _LoginScreenState extends ConsumerState<LoginScreen> {
  final TextEditingController _emailController = TextEditingController();
  final TextEditingController _passwordController = TextEditingController();
  bool _isPasswordVisible = false;

  @override
  void dispose() {
    _emailController.dispose();
    _passwordController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final authState = ref.watch(authControllerProvider);
    final isDemo = ref.watch(isDemoProvider);
    final canSubmit = !isDemo &&
        isValidEmail(_emailController.text) &&
        _passwordController.text.isNotEmpty &&
        !authState.isBusy;

    return AuthFlowScaffold(
      title: 'Nice to see you again.',
      notice: isDemo
          ? const AuthModeNoticeCard(
              title: 'Demo mode is active',
              message:
                  'Payabo could not reach the API, so live sign-in is unavailable. Use Access in demo mode to continue through the guided setup.',
              icon: Icons.wifi_off_rounded,
            )
          : null,
      onClose: () => context.go('/intro'),
      useWarmBackground: true,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          PayaboTextField(
            controller: _emailController,
            variant: PayaboInputVariant.floating,
            label: 'Email',
            enabled: !isDemo,
            keyboardType: TextInputType.emailAddress,
            onChanged: (_) => setState(() {}),
          ),
          const SizedBox(height: PayaboSpacing.md),
          PayaboTextField(
            controller: _passwordController,
            variant: PayaboInputVariant.floating,
            label: 'Password',
            enabled: !isDemo,
            obscureText: !_isPasswordVisible,
            onChanged: (_) => setState(() {}),
            suffixIcon: IconButton(
              onPressed: isDemo
                  ? null
                  : () {
                      setState(() {
                        _isPasswordVisible = !_isPasswordVisible;
                      });
                    },
              icon: Icon(_isPasswordVisible
                  ? Icons.visibility_off_outlined
                  : Icons.visibility_outlined),
            ),
          ),
          const SizedBox(height: PayaboSpacing.xl),
          PayaboButton(
            label: 'Login',
            onPressed: canSubmit
                ? () async {
                    try {
                      await ref
                          .read(authControllerProvider.notifier)
                          .signInWithPassword(
                            email: _emailController.text.trim(),
                            password: _passwordController.text,
                          );

                      if (!context.mounted) {
                        return;
                      }

                      context.go('/');
                    } catch (error) {
                      if (!context.mounted) {
                        return;
                      }

                      final message = error is ApiException
                          ? error.message
                          : 'Unable to sign in right now. Please try again.';

                      ScaffoldMessenger.of(context)
                        ..hideCurrentSnackBar()
                        ..showSnackBar(
                          SnackBar(content: Text(message)),
                        );
                    }
                  }
                : null,
          ),
          const SizedBox(height: PayaboSpacing.md),
          PayaboButton(
            label: 'Access in demo mode',
            variant: PayaboButtonVariant.secondary,
            onPressed: authState.isBusy ? null : _accessDemoMode,
          ),
          const SizedBox(height: PayaboSpacing.md),
          const _GoogleLoginButton(
            enabled: false,
            onPressed: null,
          ),
          const SizedBox(height: PayaboSpacing.lg),
          TextButton(
            onPressed:
                isDemo ? null : () => context.go('/auth/forgot-password'),
            child: const Text('Forgot your password?'),
          ),
        ],
      ),
    );
  }

  Future<void> _accessDemoMode() async {
    ref.read(isDemoProvider.notifier).state = true;
    await clearSetupCompleted(ref);

    try {
      await ref.read(authControllerProvider.notifier).signInWithPassword(
            email: 'demo@payabo.app',
            password: 'demo-access',
          );

      if (!mounted) {
        return;
      }

      context.go('/setup');
    } catch (error) {
      if (!mounted) {
        return;
      }

      final message = error is ApiException
          ? error.message
          : 'Unable to start demo mode right now. Please try again.';

      ScaffoldMessenger.of(context)
        ..hideCurrentSnackBar()
        ..showSnackBar(
          SnackBar(content: Text(message)),
        );
    }
  }
}

class _GoogleLoginButton extends StatelessWidget {
  const _GoogleLoginButton({
    required this.enabled,
    required this.onPressed,
  });

  final bool enabled;
  final VoidCallback? onPressed;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;

    return SizedBox(
      height: 52,
      child: Material(
        color: enabled ? Colors.white : const Color(0xFFF4F4F4),
        elevation: enabled ? 1 : 0,
        shadowColor: const Color(0x14000000),
        shape: RoundedRectangleBorder(
          borderRadius: PayaboRadii.radiusSm,
          side: BorderSide(
            color: enabled ? const Color(0xFFDADCE0) : const Color(0xFFE5E7EB),
          ),
        ),
        child: InkWell(
          onTap: onPressed,
          borderRadius: PayaboRadii.radiusSm,
          child: Padding(
            padding: const EdgeInsets.symmetric(horizontal: PayaboSpacing.lg),
            child: Row(
              mainAxisAlignment: MainAxisAlignment.center,
              children: <Widget>[
                Opacity(
                  opacity: enabled ? 1 : 0.6,
                  child: const _GoogleMark(),
                ),
                const SizedBox(width: PayaboSpacing.md),
                Text(
                  'Continue with Google',
                  style: Theme.of(context).textTheme.titleSmall?.copyWith(
                        color: enabled ? const Color(0xFF202124) : c.textMuted,
                        fontWeight: FontWeight.w600,
                        letterSpacing: 0,
                      ),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

class _GoogleMark extends StatelessWidget {
  const _GoogleMark();

  static const String _googleMarkSvg = '''
<svg width="18" height="18" viewBox="0 0 24 24" xmlns="http://www.w3.org/2000/svg">
  <path fill="#EA4335" d="M12 10.2v3.9h5.4c-.2 1.3-1.6 3.9-5.4 3.9-3.2 0-5.9-2.7-5.9-6s2.7-6 5.9-6c1.8 0 3.1.8 3.8 1.4l2.6-2.5C16.7 3.3 14.6 2.4 12 2.4 6.9 2.4 2.8 6.5 2.8 11.6S6.9 20.8 12 20.8c6.1 0 9.2-4.3 9.2-8.8 0-.6-.1-1.1-.2-1.8H12z"/>
  <path fill="#4285F4" d="M21.2 12c0-.6-.1-1.1-.2-1.8H12v3.9h5.4c-.3 1.5-1.1 2.7-2.3 3.5l3.6 2.8c2.1-1.9 3.3-4.8 3.3-8.4z"/>
  <path fill="#FBBC05" d="M6.1 13.7c-.2-.6-.4-1.3-.4-2.1s.1-1.4.4-2.1L2.5 6.7C1.9 8 1.5 9.3 1.5 11.6s.4 3.6 1 4.9l3.6-2.8z"/>
  <path fill="#34A853" d="M12 20.8c2.6 0 4.8-.9 6.4-2.4l-3.6-2.8c-1 .7-2.2 1.2-3.8 1.2-3.2 0-5.9-2.7-5.9-6 0-.8.2-1.5.4-2.1L2 6C1.1 7.7.6 9.6.6 11.6.6 16.7 4.7 20.8 12 20.8z"/>
</svg>
''';

  @override
  Widget build(BuildContext context) {
    return SvgPicture.string(
      _googleMarkSvg,
      width: 18,
      height: 18,
    );
  }
}
