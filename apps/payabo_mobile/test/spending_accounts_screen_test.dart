import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:payabo_mobile/app/demo/demo_data_mode.dart';
import 'package:payabo_mobile/features/spending/presentation/spending_accounts_screen.dart';

import 'test_helpers.dart';

void main() {
  testWidgets('spending accounts screen renders linked account cards',
      (WidgetTester tester) async {
    await tester.pumpWidget(buildTestApp(const SpendingAccountsScreen()));
    await tester.pumpAndSettle();

    expect(find.text('Connected accounts'), findsOneWidget);
    expect(find.text('CONNECT BANK ACCOUNT'), findsOneWidget);

    final Finder primaryScrollable = find.byType(Scrollable).last;

    await tester.scrollUntilVisible(
      find.text('Everyday current'),
      240,
      scrollable: primaryScrollable,
    );
    expect(find.text('Everyday current'), findsOneWidget);

    await tester.scrollUntilVisible(
      find.text('Bills card'),
      260,
      scrollable: primaryScrollable,
    );
    expect(find.text('Bills card'), findsOneWidget);

    await tester.scrollUntilVisible(
      find.text('Travel cash wallet'),
      220,
      scrollable: primaryScrollable,
    );
    expect(find.text('Travel cash wallet'), findsOneWidget);
  });

  testWidgets(
      'spending accounts screen shows fresh empty state in fresh demo mode',
      (WidgetTester tester) async {
    await tester.pumpWidget(
      buildTestApp(
        const SpendingAccountsScreen(),
        demoDataMode: DemoDataMode.fresh,
      ),
    );
    await tester.pumpAndSettle();

    final Finder primaryScrollable = find.byType(Scrollable).last;

    await tester.scrollUntilVisible(
      find.text('Fresh accounts state'),
      320,
      scrollable: primaryScrollable,
    );

    expect(find.text('Fresh accounts state'), findsOneWidget);
    expect(find.text('Everyday current'), findsNothing);
  });

  testWidgets('connect bank account flow links accounts in fresh demo mode',
      (WidgetTester tester) async {
    await tester.pumpWidget(
      buildTestApp(
        const SpendingAccountsScreen(),
        demoDataMode: DemoDataMode.fresh,
      ),
    );
    await tester.pumpAndSettle();

    final Finder primaryScrollable = find.byType(Scrollable).last;

    final Finder connectButton = find.text('CONNECT BANK');

    await tester.scrollUntilVisible(
      connectButton,
      160,
      scrollable: primaryScrollable,
    );
    await tester.tap(connectButton);
    await tester.pumpAndSettle();

    expect(find.text('Connect bank account'), findsOneWidget);
    expect(find.byKey(const Key('accounts-connect-continue')), findsOneWidget);

    await tester.tap(find.byKey(const Key('accounts-connect-continue')));
    await tester.pumpAndSettle(const Duration(seconds: 3));

    expect(find.text('Connected current'), findsOneWidget);
    expect(find.text('Fresh accounts state'), findsNothing);
  });

  testWidgets('refresh action updates a linked account card',
      (WidgetTester tester) async {
    await tester.pumpWidget(buildTestApp(const SpendingAccountsScreen()));
    await tester.pumpAndSettle();

    final Finder primaryScrollable = find.byType(Scrollable).last;
    final Finder everydayCard =
        find.byKey(const Key('account-card-everyday-current'));

    await tester.scrollUntilVisible(
      everydayCard,
      220,
      scrollable: primaryScrollable,
    );

    final Finder refreshButton = find.descendant(
      of: everydayCard,
      matching: find.text('REFRESH'),
    );

    await tester.scrollUntilVisible(
      refreshButton,
      140,
      scrollable: primaryScrollable,
    );
    await tester.ensureVisible(refreshButton);
    await tester.pumpAndSettle();
    await tester.tap(refreshButton);
    await tester.pumpAndSettle();

    expect(find.text('Synced just now'), findsWidgets);
  });

  testWidgets('reconnect action restores an attention account',
      (WidgetTester tester) async {
    await tester.pumpWidget(buildTestApp(const SpendingAccountsScreen()));
    await tester.pumpAndSettle();

    final Finder primaryScrollable = find.byType(Scrollable).last;
    final Finder billsCard = find.byKey(const Key('account-card-bills-card'));

    await tester.scrollUntilVisible(
      billsCard,
      260,
      scrollable: primaryScrollable,
    );

    final Finder reconnectButton = find.descendant(
      of: billsCard,
      matching: find.text('RECONNECT'),
    );

    await tester.scrollUntilVisible(
      reconnectButton,
      140,
      scrollable: primaryScrollable,
    );
    await tester.ensureVisible(reconnectButton);
    await tester.pumpAndSettle();
    await tester.tap(reconnectButton);
    await tester.pumpAndSettle();

    await tester.tap(find.byKey(const Key('accounts-connect-continue')));
    await tester.pumpAndSettle(const Duration(seconds: 3));

    expect(
      find.text(
        'Connection restored. Spend can use fresh transactions and balances again.',
      ),
      findsOneWidget,
    );
  });

  testWidgets('disconnect action removes a linked account from active spend',
      (WidgetTester tester) async {
    await tester.pumpWidget(buildTestApp(const SpendingAccountsScreen()));
    await tester.pumpAndSettle();

    final Finder primaryScrollable = find.byType(Scrollable).last;
    final Finder everydayCard =
        find.byKey(const Key('account-card-everyday-current'));

    await tester.scrollUntilVisible(
      everydayCard,
      220,
      scrollable: primaryScrollable,
    );

    final Finder disconnectButton = find.descendant(
      of: everydayCard,
      matching: find.text('DISCONNECT'),
    );

    await tester.scrollUntilVisible(
      disconnectButton,
      140,
      scrollable: primaryScrollable,
    );
    await tester.ensureVisible(disconnectButton);
    await tester.pumpAndSettle();
    await tester.tap(disconnectButton);
    await tester.pumpAndSettle();
    await tester.tap(find.text('Disconnect'));
    await tester.pumpAndSettle();

    expect(find.text('Everyday current'), findsNothing);
  });
}
