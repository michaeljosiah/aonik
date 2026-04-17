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

    // The sheet's ListView uses SliverChildListDelegate, which lazily builds
    // children that fall outside the viewport. Only the first account row is
    // in the widget tree initially — drag the sheet to bring the others in
    // via cacheExtent as they approach the viewport.
    expect(find.text('UK Current'), findsOneWidget);

    final Finder sheetScrollable = find.byType(Scrollable).last;

    Future<void> dragToReveal(Finder target) async {
      for (var i = 0; i < 20 && target.evaluate().isEmpty; i++) {
        await tester.drag(sheetScrollable, const Offset(0, -220));
        await tester.pumpAndSettle();
      }
    }

    await dragToReveal(find.byKey(const Key('account-card-uk-credit-card')));
    expect(
        find.byKey(const Key('account-card-uk-credit-card')), findsOneWidget);

    await dragToReveal(find.text('Travel cash wallet'));
    expect(find.text('Travel cash wallet'), findsOneWidget);

    await dragToReveal(find.byKey(const Key('accounts-connect-sheet')));
    expect(find.byKey(const Key('accounts-connect-sheet')), findsOneWidget);
    // Label is uppercased by PayaboButton at paint time.
    expect(find.text('CONNECT BANK ACCOUNT'), findsOneWidget);
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
    expect(find.text('UK Current'), findsNothing);
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

    // Empty-state hero renders the "Connect bank account" CTA as
    // `accounts-connect-primary`. Tap via its key so the finder doesn't
    // rely on PayaboButton's label casing.
    final Finder connectButton =
        find.byKey(const Key('accounts-connect-primary'));
    await tester.ensureVisible(connectButton);
    await tester.tap(connectButton);
    await tester.pumpAndSettle();

    expect(find.text('Connect bank account'), findsOneWidget);
    expect(find.byKey(const Key('accounts-country-dropdown')), findsOneWidget);
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
        find.byKey(const Key('account-card-uk-everyday-current'));

    await tester.scrollUntilVisible(
      everydayCard,
      220,
      scrollable: primaryScrollable,
    );

    // PayaboButton uppercases its label at paint time.
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

    // Successful refresh surfaces a "Refreshed N linked account(s) from X"
    // snack-bar. Avoid `pumpAndSettle()` here — SnackBar auto-dismisses
    // after 4 s and settle would advance past that. uk-everyday-current
    // and uk-savings share mock-connection-starling so the count is 2.
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 200));
    expect(
      find.textContaining('Refreshed 2 linked accounts from Starling Bank'),
      findsOneWidget,
    );
  });

  testWidgets('reconnect action restores an attention account',
      (WidgetTester tester) async {
    await tester.pumpWidget(buildTestApp(const SpendingAccountsScreen()));
    await tester.pumpAndSettle();

    final Finder sheetScrollable = find.byType(Scrollable).last;
    final Finder billsCard = find.byKey(const Key('account-card-uk-credit-card'));

    // Bring the uk-credit-card row into the viewport via drags (the sheet's
    // SliverList builds children lazily, so scrollUntilVisible without a
    // prior match throws on element.single).
    for (var i = 0; i < 20 && billsCard.evaluate().isEmpty; i++) {
      await tester.drag(sheetScrollable, const Offset(0, -220));
      await tester.pumpAndSettle();
    }
    expect(billsCard, findsOneWidget);

    final Finder reconnectButton = find.descendant(
      of: billsCard,
      matching: find.text('RECONNECT'),
    );

    await tester.ensureVisible(reconnectButton);
    await tester.pumpAndSettle();
    await tester.tap(reconnectButton);
    await tester.pumpAndSettle();

    await tester.tap(find.byKey(const Key('accounts-connect-continue')));
    // Allow the sheet-exchange animation + provider refresh to complete,
    // but avoid settling past the auto-dismissing snack-bar.
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 600));
    await tester.pump(const Duration(milliseconds: 600));

    // Reconnect flows through the same sheet as connect and surfaces the
    // "Connected N account(s) from X" snack-bar on completion.
    expect(
      find.textContaining('Connected 1 account'),
      findsOneWidget,
    );
  });

  testWidgets('disconnect action removes a linked account from active spend',
      (WidgetTester tester) async {
    await tester.pumpWidget(buildTestApp(const SpendingAccountsScreen()));
    await tester.pumpAndSettle();

    final Finder primaryScrollable = find.byType(Scrollable).last;
    final Finder everydayCard =
        find.byKey(const Key('account-card-uk-everyday-current'));

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

    expect(find.text('UK Current'), findsNothing);
  });
}
