import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:payabo_mobile/app/demo/demo_data_mode.dart';
import 'package:payabo_mobile/features/chat/presentation/chat_history_screen.dart';

import 'test_helpers.dart';

void main() {
  testWidgets('chat history screen renders and filters conversations',
      (WidgetTester tester) async {
    await tester.pumpWidget(buildTestApp(const ChatHistoryScreen()));
    await tester.pumpAndSettle();

    expect(find.text('Conversation history'), findsOneWidget);
    expect(find.text('Every thread with Simi, ready to pick back up.'),
        findsOneWidget);
    expect(find.text('Search conversations'), findsOneWidget);
    expect(find.text('Current account balance inquiry'), findsOneWidget);

    await tester.enterText(find.byType(TextField).first, 'track spending');
    await tester.pumpAndSettle();

    expect(find.text('Track spending to see where money goes'), findsOneWidget);
    expect(find.text('Current account balance inquiry'), findsNothing);
  });

  testWidgets('chat history is empty in fresh demo mode',
      (WidgetTester tester) async {
    await tester.pumpWidget(
      buildTestApp(
        const ChatHistoryScreen(),
        demoDataMode: DemoDataMode.fresh,
      ),
    );
    await tester.pumpAndSettle();

    expect(
      find.text('No conversation history yet in this demo state.'),
      findsOneWidget,
    );
    expect(find.text('Current account balance inquiry'), findsNothing);
  });

  testWidgets('chat history applies dark theme containers',
      (WidgetTester tester) async {
    await tester.pumpWidget(
      buildTestApp(
        const ChatHistoryScreen(),
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
            (widget.decoration as BoxDecoration).gradient != null,
      ),
      findsWidgets,
    );
  });
}
