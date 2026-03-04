import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

class SplashScreen extends StatelessWidget {
  const SplashScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: Colors.white,
      body: SafeArea(
        child: Center(
          child: InkWell(
            onTap: () => context.go('/intro'),
            borderRadius: BorderRadius.circular(12),
            child: Padding(
              padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 16),
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: <Widget>[
                  Image.asset(
                    'assets/images/mba_logo.png',
                    width: 260,
                    fit: BoxFit.contain,
                  ),
                  const SizedBox(height: 18),
                  const Text('Tap logo to continue'),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}
