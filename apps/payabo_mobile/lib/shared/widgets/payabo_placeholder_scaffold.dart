import 'package:flutter/material.dart';

import '../theme/payabo_spacing.dart';

class PayaboPlaceholderScaffold extends StatelessWidget {
  const PayaboPlaceholderScaffold({
    super.key,
    required this.title,
    required this.subtitle,
    required this.child,
    this.actions = const <Widget>[],
  });

  final String title;
  final String subtitle;
  final Widget child;
  final List<Widget> actions;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: Text(title),
        actions: actions,
      ),
      body: SafeArea(
        child: SingleChildScrollView(
          padding: PayaboSpacing.page,
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: <Widget>[
              Text(
                subtitle,
                style: Theme.of(context).textTheme.bodyMedium,
              ),
              const SizedBox(height: PayaboSpacing.lg),
              child,
            ],
          ),
        ),
      ),
    );
  }
}
