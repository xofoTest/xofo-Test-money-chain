export function money(paise: number): string {
  const inr = paise / 100;
  const sign = inr < 0 ? '-' : '';
  const abs = Math.abs(inr);
  const str = Number.isInteger(abs) ? abs.toLocaleString('en-IN') : abs.toFixed(2);
  return `${sign}₹${str}`;
}

export function initials(name?: string): string {
  const parts = (name || '').trim().split(/\s+/);
  return ((parts[0] || '?')[0] + (parts[1] ? parts[1][0] : '')).toUpperCase();
}

export function statusClass(status: string): string {
  if (status === 'active' || status === 'approved') return 'badge-green';
  if (status === 'suspended') return 'badge-red';
  if (status === 'settled') return 'badge-green';
  return 'badge-amber';
}

export function amountClass(paise: number): string {
  if (paise < 0) return 'amount neg';
  if (paise > 0) return 'amount pos';
  return 'amount';
}
