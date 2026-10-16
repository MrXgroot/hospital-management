import dayjs from 'dayjs';
import type { IsoDate, TimeOfDay, Timestamp } from '../types/api';

const inr = new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR', maximumFractionDigits: 2 });

export const formatMoney = (amount: number | null | undefined) => inr.format(amount ?? 0);

/** Today's date in the browser's time zone (the hospital runs in a single zone). */
export const todayIso = (): IsoDate => dayjs().format('YYYY-MM-DD');

export const addDaysIso = (date: IsoDate, days: number): IsoDate => dayjs(date).add(days, 'day').format('YYYY-MM-DD');

export const formatDate = (date: IsoDate | Timestamp | null | undefined) => (date ? dayjs(date).format('DD MMM YYYY') : '—');

export const formatDateTime = (ts: Timestamp | null | undefined) => (ts ? dayjs(toUtc(ts)).format('DD MMM YYYY, hh:mm A') : '—');

export const formatTimeOfDay = (time: TimeOfDay | null | undefined) =>
  time ? dayjs(`2000-01-01T${time}`).format('hh:mm A') : '—';

export const formatTimestampTime = (ts: Timestamp | null | undefined) => (ts ? dayjs(toUtc(ts)).format('hh:mm A') : '—');

/** API timestamps are UTC; older serialisers may omit the trailing Z. */
const toUtc = (ts: string) => (/[zZ]|[+-]\d\d:\d\d$/.test(ts) ? ts : `${ts}Z`);

export const WEEKDAYS = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'];

export const ageGender = (age: number | null | undefined, gender: string) => `${age ?? '?'} y / ${gender}`;

export const emptyToNull = (value: string | null | undefined) => {
  const trimmed = value?.trim();
  return trimmed ? trimmed : null;
};

export const toNumberOrNull = (value: string | null | undefined) => {
  const trimmed = value?.trim();
  if (!trimmed) return null;
  const n = Number(trimmed);
  return Number.isFinite(n) ? n : null;
};
