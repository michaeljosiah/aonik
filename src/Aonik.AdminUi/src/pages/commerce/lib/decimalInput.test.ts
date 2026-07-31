import { describe, expect, it } from 'vitest';

import { NUTRITION_FIGURE_RULE, validateDecimalInput } from './decimalInput';

const FIGURE = NUTRITION_FIGURE_RULE;

describe('validateDecimalInput', () => {
  it('accepts a value at the column scale and rejects one beyond it', () => {
    // decimal(9,2) does not refuse a third decimal — it ROUNDS it — so a save would report
    // success for a figure the store does not hold.
    expect(validateDecimalInput('12.34', FIGURE)).toBeNull();
    expect(validateDecimalInput('12.345', FIGURE)).toMatch(/2 decimal places/);
  });

  it('rejects exponent notation, which has no decimal point but real decimals', () => {
    // Number('1e-5') is finite, and the string contains no ".", so a scale check derived from
    // the coerced number would see zero decimals and let it through.
    expect(validateDecimalInput('1e-5', FIGURE)).toMatch(/plain number/);
    expect(validateDecimalInput('1E5', FIGURE)).toMatch(/plain number/);
  });

  it('rejects other things Number() happily coerces', () => {
    expect(validateDecimalInput('0x10', FIGURE)).toMatch(/plain number/);
    expect(validateDecimalInput('Infinity', FIGURE)).toMatch(/plain number/);
    expect(validateDecimalInput('1_000', FIGURE)).toMatch(/plain number/);
  });

  it('rejects negatives', () => {
    expect(validateDecimalInput('-1', FIGURE)).toMatch(/cannot be negative/);
  });

  it("enforces the column's own maximum", () => {
    expect(validateDecimalInput('9999999.99', FIGURE)).toBeNull();
    expect(validateDecimalInput('10000000', FIGURE)).toMatch(/larger than the stored maximum/);
  });

  it('treats blank as the CALLER’s business, not an authored zero', () => {
    // Number('') is 0. Callers that mean "not published" must check blank themselves — this
    // returns null so a blank optional field is not reported as invalid.
    expect(validateDecimalInput('', FIGURE)).toBeNull();
    expect(validateDecimalInput('   ', FIGURE)).toBeNull();
  });

  it('counts SIGNIFICANT digits, so leading zeros do not consume the budget', () => {
    expect(validateDecimalInput('000000012.50', FIGURE)).toBeNull();
  });

  it('adapts to a different scale without a second copy of the rule', () => {
    const money = { scale: 4, subject: 'A surcharge' };
    expect(validateDecimalInput('1.2345', money)).toBeNull();
    expect(validateDecimalInput('1.23456', money)).toMatch(/4 decimal places/);
    // The same input, judged by the two columns that actually store it.
    expect(validateDecimalInput('1.2345', FIGURE)).toMatch(/2 decimal places/);
  });

  it('singularises a scale of one', () => {
    expect(validateDecimalInput('1.23', { scale: 1, subject: 'X' })).toMatch(/1 decimal place —/);
  });
});
