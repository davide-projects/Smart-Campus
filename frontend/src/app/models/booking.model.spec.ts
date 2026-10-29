import {
  campusDateTime,
  campusTime,
  campusToday,
  defaultSearchSlot,
  formatTimeRange,
  halfHourSlots,
  isInCheckInWindow,
  reservationTimeLabel,
  toMinutes,
} from './booking.model';

// Variabile d'ambiente del fuso del processo di test, per simulare un dispositivo in un altro fuso.
const env = (globalThis as unknown as { process: { env: Record<string, string | undefined> } })
  .process.env;

describe('campus time helpers', () => {
  const originalTimeZone = env['TZ'];

  afterEach(() => {
    if (originalTimeZone === undefined) {
      delete env['TZ'];
    } else {
      env['TZ'] = originalTimeZone;
    }
  });

  describe('campusDateTime', () => {
    it('uses +01:00 in winter (standard time)', () => {
      expect(campusDateTime(new Date(2026, 10, 5), '10:00')).toBe('2026-11-05T10:00:00+01:00');
      expect(campusDateTime(new Date(2026, 10, 5), '11:30')).toBe('2026-11-05T11:30:00+01:00');
    });

    it('uses +02:00 in summer (daylight saving time)', () => {
      expect(campusDateTime(new Date(2026, 5, 5), '10:00')).toBe('2026-06-05T10:00:00+02:00');
    });

    it('does not depend on the time zone of the device', () => {
      for (const zone of ['UTC', 'America/New_York', 'Asia/Tokyo']) {
        env['TZ'] = zone;
        // Il giorno scelto nel calendario è una data locale del dispositivo.
        expect(campusDateTime(new Date(2026, 10, 5), '10:00')).toBe('2026-11-05T10:00:00+01:00');
        expect(campusDateTime(new Date(2026, 5, 5), '10:00')).toBe('2026-06-05T10:00:00+02:00');
      }
    });

    it('describes the same instant as the campus wall clock', () => {
      env['TZ'] = 'UTC';
      expect(new Date(campusDateTime(new Date(2026, 10, 5), '10:00')).toISOString()).toBe(
        '2026-11-05T09:00:00.000Z',
      );
    });

    it('follows the switch to daylight saving time on the day itself', () => {
      // Il 29 marzo 2026 Roma passa a +02:00 alle 02:00.
      expect(campusDateTime(new Date(2026, 2, 29), '01:30')).toBe('2026-03-29T01:30:00+01:00');
      expect(campusDateTime(new Date(2026, 2, 29), '10:00')).toBe('2026-03-29T10:00:00+02:00');
    });
  });

  describe('campusToday', () => {
    it('returns the date of Rome even when the device is still on the previous day', () => {
      env['TZ'] = 'America/New_York';
      // Alle 00:30 di Roma del 5 novembre a New York è ancora il 4.
      const today = campusToday(new Date('2026-11-04T23:30:00Z'));

      expect([today.getFullYear(), today.getMonth(), today.getDate()]).toEqual([2026, 10, 5]);
      expect([today.getHours(), today.getMinutes()]).toEqual([0, 0]);
    });
  });

  describe('campusTime', () => {
    it('returns the time of Rome in winter and in summer', () => {
      expect(campusTime(new Date('2026-11-05T08:10:00Z'))).toBe('09:10');
      expect(campusTime(new Date('2026-06-05T08:10:00Z'))).toBe('10:10');
    });

    it('writes the hour after midnight as 00', () => {
      expect(campusTime(new Date('2026-11-04T23:30:00Z'))).toBe('00:30');
    });

    it('does not depend on the time zone of the device', () => {
      for (const zone of ['UTC', 'America/New_York', 'Asia/Tokyo']) {
        env['TZ'] = zone;
        expect(campusTime(new Date('2026-11-05T08:10:00Z'))).toBe('09:10');
      }
    });
  });

  describe('reservationTimeLabel', () => {
    it('formats the confirmation in Italian and in campus time', () => {
      env['TZ'] = 'UTC';
      expect(
        reservationTimeLabel({
          start: '2026-11-05T10:00:00+01:00',
          end: '2026-11-05T11:00:00+01:00',
        }),
      ).toBe('giovedì 5 novembre 2026, 10:00–11:00');
    });

    it('formats a summer reservation with daylight saving time', () => {
      env['TZ'] = 'UTC';
      expect(
        reservationTimeLabel({
          start: '2026-06-05T10:00:00+02:00',
          end: '2026-06-05T11:00:00+02:00',
        }),
      ).toBe('venerdì 5 giugno 2026, 10:00–11:00');
    });
  });

  describe('formatTimeRange', () => {
    it('formats the time range in campus time', () => {
      env['TZ'] = 'UTC';
      expect(formatTimeRange('2026-11-05T09:00:00+01:00', '2026-11-05T10:30:00+01:00')).toBe(
        '09:00 – 10:30',
      );
    });
  });

  describe('halfHourSlots', () => {
    it('generates half-hour slots between opening and closing hours', () => {
      const slots = halfHourSlots('08:00', '10:00');
      expect(slots).toEqual(['08:00', '08:30', '09:00', '09:30', '10:00']);
    });
  });

  describe('toMinutes', () => {
    it('converts HH:mm to minutes from midnight', () => {
      expect(toMinutes('08:30')).toBe(510);
      expect(toMinutes('20:00')).toBe(1200);
    });
  });

  describe('defaultSearchSlot', () => {
    it('proposes the next half hour after the current campus time', () => {
      // 09:10 -> next start 09:30, end 10:30
      const slot = defaultSearchSlot(new Date('2026-11-05T08:10:00Z')); // 09:10 in Rome
      expect(slot).toEqual({ start: '09:30', end: '10:30' });
    });

    it('caps the end time at 20:00 when start is near campus closing', () => {
      // 19:10 in Rome -> next start 19:30, end 20:00
      const slot = defaultSearchSlot(new Date('2026-11-05T18:10:00Z'));
      expect(slot).toEqual({ start: '19:30', end: '20:00' });
    });

    it('falls back to 09:00-10:00 when campus is closed for the day', () => {
      // 21:00 in Rome
      const slot = defaultSearchSlot(new Date('2026-11-05T20:00:00Z'));
      expect(slot).toEqual({ start: '09:00', end: '10:00' });
    });
  });

  describe('isInCheckInWindow', () => {
    // Inizio alle 10:00 del campus (09:00Z a novembre).
    const item = { start: '2026-11-05T10:00:00+01:00' };
    const at = (iso: string) => Date.parse(iso);

    it('includes both ends of the window', () => {
      expect(isInCheckInWindow(item, at('2026-11-05T08:45:00Z'))).toBe(true);
      expect(isInCheckInWindow(item, at('2026-11-05T09:00:00Z'))).toBe(true);
      expect(isInCheckInWindow(item, at('2026-11-05T09:15:00Z'))).toBe(true);
    });

    it('excludes one millisecond before and after the window', () => {
      expect(isInCheckInWindow(item, at('2026-11-05T08:44:59.999Z'))).toBe(false);
      expect(isInCheckInWindow(item, at('2026-11-05T09:15:00.001Z'))).toBe(false);
    });

    it('excludes a reservation of the next day', () => {
      expect(isInCheckInWindow(item, at('2026-11-04T09:00:00Z'))).toBe(false);
    });

    it('does not depend on the time zone of the device', () => {
      for (const zone of ['UTC', 'America/New_York', 'Asia/Tokyo']) {
        env['TZ'] = zone;
        expect(isInCheckInWindow(item, at('2026-11-05T08:45:00Z'))).toBe(true);
        expect(isInCheckInWindow(item, at('2026-11-05T08:44:59.999Z'))).toBe(false);
        expect(isInCheckInWindow(item, at('2026-11-05T09:15:00Z'))).toBe(true);
        expect(isInCheckInWindow(item, at('2026-11-05T09:15:00.001Z'))).toBe(false);
      }
    });
  });
});
