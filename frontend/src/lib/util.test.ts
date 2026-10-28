import { describe, expect, it } from 'vitest';
import { amountClass, initials, money, statusClass } from './util';
import { en, ml } from './i18n';

describe('money (integer paise -> rupees)', () => {
  it('formats whole rupees without decimals', () => {
    expect(money(5000)).toBe('₹50');
    expect(money(10000)).toBe('₹100');
    expect(money(1000)).toBe('₹10');
    expect(money(0)).toBe('₹0');
  });
  it('formats negative amounts with a sign', () => {
    expect(money(-10000)).toBe('-₹100');
    expect(money(-1000)).toBe('-₹10');
  });
  it('formats fractional rupees with two decimals', () => {
    expect(money(1050)).toBe('₹10.50');
  });
});

describe('statusClass', () => {
  it('maps statuses to badge tones', () => {
    expect(statusClass('active')).toBe('badge-green');
    expect(statusClass('suspended')).toBe('badge-red');
    expect(statusClass('pending')).toBe('badge-amber');
    expect(statusClass('approved')).toBe('badge-green');
  });
});

describe('initials', () => {
  it('takes the first letter of the first two words', () => {
    expect(initials('Arun Nair')).toBe('AN');
    expect(initials('Gouri')).toBe('G');
  });
});

describe('amountClass', () => {
  it('colors negatives red and positives green', () => {
    expect(amountClass(-1)).toBe('amount neg');
    expect(amountClass(1)).toBe('amount pos');
    expect(amountClass(0)).toBe('amount');
  });
});

describe('i18n parity (English & Malayalam)', () => {
  it('en and ml expose exactly the same keys', () => {
    expect(Object.keys(ml).sort()).toEqual(Object.keys(en).sort());
  });
  it('every en key has a non-empty ml translation', () => {
    for (const key of Object.keys(en)) {
      expect(ml[key], `missing ml translation for ${key}`).toBeTruthy();
    }
  });
});
