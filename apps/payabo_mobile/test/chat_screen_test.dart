import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:payabo_mobile/app/demo/demo_data_mode.dart';
import 'package:payabo_mobile/features/chat/presentation/chat_screen.dart';

import 'test_helpers.dart';

void main() {
  testWidgets('chat screen renders seeded conversation and money plans',
      (WidgetTester tester) async {
    await tester.pumpWidget(buildTestApp(const ChatScreen()));
    await tester.pumpAndSettle();

    final Finder primaryScrollable = find.byType(Scrollable).first;

    expect(find.byIcon(Icons.menu_rounded), findsOneWidget);
    expect(find.textContaining('Hey'), findsOneWidget);
    expect(find.text('Sunday reset'), findsWidgets);
    expect(find.text('Write here...'), findsOneWidget);
    expect(find.text('Attach'), findsOneWidget);
    expect(find.text('Camera'), findsOneWidget);
    expect(find.text('Voice'), findsOneWidget);

    await tester.enterText(find.byType(TextField), 'Help me catch up on bills');
    await tester.pump();
    await tester.tap(find.byIcon(Icons.send_rounded));
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 300));
    await tester.pumpAndSettle();

    await tester.drag(primaryScrollable, const Offset(0, -600));
    await tester.pumpAndSettle();

    expect(find.text('Simi'), findsOneWidget);
    expect(find.text('Bill rescue plan'), findsWidgets);
    expect(find.text('Pin every due date in one list.'), findsOneWidget);
  });

  testWidgets('chat screen starts empty in fresh demo mode',
      (WidgetTester tester) async {
    await tester.pumpWidget(
      buildTestApp(
        const ChatScreen(),
        demoDataMode: DemoDataMode.fresh,
      ),
    );
    await tester.pumpAndSettle();

    expect(find.text('Hello!'), findsOneWidget);
    expect(find.text('I\'M SIMI'), findsOneWidget);
    expect(
      find.textContaining('I am here to guide you through bills, budgets'),
      findsOneWidget,
    );
    expect(
        find.text('Try asking "How do I stop missing bills?"'), findsOneWidget);
    expect(find.text('Sunday reset'), findsNothing);

    final Text welcomeText = tester.widget<Text>(find.text('Hello!'));
    expect(welcomeText.style?.fontSize, greaterThanOrEqualTo(42));
  });

  testWidgets('chat screen applies dark theme chat surfaces',
      (WidgetTester tester) async {
    await tester.pumpWidget(
      buildTestApp(
        const ChatScreen(),
        themeMode: ThemeMode.dark,
      ),
    );
    await tester.pumpAndSettle();

    final Scaffold scaffold = tester.widget<Scaffold>(find.byType(Scaffold));

    expect(scaffold.backgroundColor, const Color(0xFF070505));
    expect(
      find.byWidgetPredicate(
        (Widget widget) =>
            widget is DecoratedBox &&
            widget.decoration is BoxDecoration &&
            (widget.decoration as BoxDecoration).color ==
                const Color(0xFF15110F),
      ),
      findsWidgets,
    );
  });
}
