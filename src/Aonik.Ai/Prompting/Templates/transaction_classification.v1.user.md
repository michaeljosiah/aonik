Classify the following transaction(s). Respond with a JSON array — one object per transaction, in the same order as the input.

Each object must have these fields:
- "id": the transaction ID (string, copied from input)
- "category": one of the valid category codes from the taxonomy
- "subCategory": a valid subcategory code from the taxonomy table, or null if uncertain
- "confidence": a number between 0.0 and 0.7

{{TRANSACTIONS_JSON}}
