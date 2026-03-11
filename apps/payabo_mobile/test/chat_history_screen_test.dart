import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:payabo_mobile/features/chat/presentation/chat_history_screen.dart';

import 'test_helpers.dart';

void main() {
  testWidgets('chat history screen renders and filters conversations',
      (WidgetTester tester) async {
    await tester.pumpWidget(buildTestApp(const ChatHistoryScreen()));
    await tester.pumpAndSettle();

    expect(find.text('Conversation history'), findsOneWidget);
    expect(find.text('Search conversations'), findsOneWidget);
    expect(find.text('Current account balance inquiry'), findsOneWidget);

    await tester.enterText(find.byType(TextField).first, 'track spending');
    await tester.pumpAndSettle();

    expect(find.text('Track spending to see where money goes'), findsOneWidget);
    expect(find.text('Current account balance inquiry'), findsNothing);
  });
}
