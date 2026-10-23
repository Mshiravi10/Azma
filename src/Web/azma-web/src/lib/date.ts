import { format, formatDistanceToNowStrict, parseISO } from 'date-fns-jalali';
import { faIR } from 'date-fns-jalali/locale';
import { toFa } from './utils';

export function fmtDate(input: string | Date | null | undefined, pattern = 'yyyy/MM/dd') {
  if (!input) return '—';
  const d = typeof input === 'string' ? parseISO(input) : input;
  return toFa(format(d, pattern, { locale: faIR }));
}

export function fmtDateTime(input: string | Date | null | undefined) {
  return fmtDate(input, 'yyyy/MM/dd HH:mm');
}

export function fmtRelative(input: string | Date | null | undefined) {
  if (!input) return '—';
  const d = typeof input === 'string' ? parseISO(input) : input;
  return toFa(formatDistanceToNowStrict(d, { addSuffix: true, locale: faIR }));
}
