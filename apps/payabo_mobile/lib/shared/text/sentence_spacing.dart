// ignore_for_file: public_member_api_docs

/// Display-time normaliser that inserts a missing space after sentence-ending
/// punctuation (`.`, `!`, `?`) when the next character is a letter.
///
/// Where this comes from: streaming LLM token boundaries (SSE `delta` events
/// and Voxa `BotTextEvent`s) sometimes split a sentence so the period lands at
/// the end of token N and the next sentence's first letter lands at the start
/// of token N+1, with no separator between them. The assembled text on the
/// client then reads `"commitments.It seems"` / `"currently.If"` etc. — a
/// purely cosmetic problem the model itself didn't intend.
///
/// We don't touch numbers (`3.14`, `$10.50`, `v1.5x`) because the next char is
/// a digit there. Ellipses (`...word`) get one space inserted after the final
/// dot, which is what most prose wants anyway.
///
/// Pure function, idempotent — running it twice on the same string is a no-op.
String normalizeSentenceSpacing(String text) {
  if (text.isEmpty) return text;
  return text.replaceAllMapped(
    _sentenceBoundary,
    (Match m) => '${m.group(1)} ${m.group(2)}',
  );
}

/// Period / exclamation / question mark immediately followed by a letter.
/// Captured groups: the punctuation and the letter; replacement re-inserts
/// them with a single space between.
final RegExp _sentenceBoundary = RegExp(r'([.!?])([A-Za-z])');
