import { campusDateTime, campusToday, reservationTimeLabel } from './booking.model';

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
});
