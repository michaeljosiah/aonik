import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:payabo_mobile/features/chat/presentation/chat_screen.dart';

import 'test_helpers.dart';

void main() {
  testWidgets('chat screen renders seeded conversation and money plans',
      (WidgetTester tester) async {
    await tester.pumpWidget(buildTestApp(const ChatScreen()));
    await tester.pumpAndSettle();

    final Finder primaryScrollable = find.byType(Scrollable).first;

    expect(find.text('Hey you'), findsOneWidget);
    expect(find.text('Sunday reset'), findsWidgets);
    expect(find.byIcon(Icons.menu_rounded), findsOneWidget);

    await tester.scrollUntilVisible(
      find.text('Build me a Sunday reset'),
      300,
      scrollable: primaryScrollable,
    );
    expect(find.text('Build me a Sunday reset'), findsOneWidget);

    await tester.enterText(find.byType(TextField), 'Help me catch up on bills');
    await tester.pump();
    await tester.tap(find.byIcon(Icons.auto_graph_rounded));
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 300));
    await tester.pumpAndSettle();

    await tester.drag(primaryScrollable, const Offset(0, -600));
    await tester.pumpAndSettle();

    expect(find.text('Bill rescue plan'), findsWidgets);
    expect(find.text('Pin every due date in one list.'), findsOneWidget);
  });
}
