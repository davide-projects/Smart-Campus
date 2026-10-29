import { rejectionText } from './rejection-message';

type Case = [code: string, limits: Record<string, unknown> | null, expected: string];

// Un caso per ogni motivo che la pagina di prenotazione può ricevere.
const CASES: Case[] = [
  ['room-occupied', null, 'Lo spazio è già prenotato in questa fascia.'],
  ['person-occupied', null, 'Hai già una prenotazione in questa fascia oraria.'],
  [
    'members-occupied',
    null,
    'Uno o più membri hanno già una prenotazione in questa fascia oraria.',
  ],
  [
    'booking-in-progress',
    null,
    "Hai un'altra prenotazione in corso, riprova tra qualche secondo.",
  ],
  ['room-closed', null, 'Lo spazio è chiuso in questo giorno.'],
  ['capacity-exceeded', null, 'I partecipanti superano la capienza dello spazio.'],
  ['room-not-bookable', null, 'Questo spazio non è prenotabile.'],
  ['invalid-time-range', null, "L'orario di fine deve essere successivo a quello di inizio."],
  ['in-the-past', null, 'Non è possibile prenotare nel passato.'],
  ['different-days', null, 'La prenotazione deve iniziare e finire nello stesso giorno.'],
  [
    'outside-opening-hours',
    { opensAt: '08:00', closesAt: '20:00' },
    'Lo spazio è aperto dalle 08:00 alle 20:00.',
  ],
  [
    'duration-exceeded',
    { maxDurationMinutes: 240 },
    'Una prenotazione può durare al massimo 4 ore.',
  ],
  ['too-far-ahead', { maxAdvanceDays: 14 }, 'Si può prenotare al massimo 14 giorni prima.'],
  [
    'slot-boundary',
    { slotMinutes: 30 },
    "Le prenotazioni iniziano e finiscono all'ora esatta o alla mezz'ora.",
  ],
  ['purpose-too-long', { maxLength: 200 }, 'Il motivo può contenere al massimo 200 caratteri.'],
];

describe('rejectionText', () => {
  it.each(CASES)('writes the message of «%s»', (code, limits, expected) => {
    expect(rejectionText({ code, limits, detail: 'testo del servizio' })).toBe(expected);
  });

  it('puts the limits in the text, so it follows the values in force', () => {
    expect(
      rejectionText({
        code: 'outside-opening-hours',
        limits: { opensAt: '09:00', closesAt: '13:00' },
        detail: null,
      }),
    ).toBe('Lo spazio è aperto dalle 09:00 alle 13:00.');
  });

  it('writes the duration in hours when it is whole and in minutes otherwise', () => {
    const text = (maxDurationMinutes: number) =>
      rejectionText({ code: 'duration-exceeded', limits: { maxDurationMinutes }, detail: null });

    expect(text(60)).toBe('Una prenotazione può durare al massimo 1 ora.');
    expect(text(120)).toBe('Una prenotazione può durare al massimo 2 ore.');
    expect(text(90)).toBe('Una prenotazione può durare al massimo 90 minuti.');
  });

  it('uses the singular for one day of advance', () => {
    expect(
      rejectionText({ code: 'too-far-ahead', limits: { maxAdvanceDays: 1 }, detail: null }),
    ).toBe('Si può prenotare al massimo 1 giorno prima.');
  });

  it('describes a slot that is not the half hour with its length', () => {
    expect(
      rejectionText({ code: 'slot-boundary', limits: { slotMinutes: 15 }, detail: null }),
    ).toBe('Le prenotazioni iniziano e finiscono ogni 15 minuti.');
  });

  it('falls back to the text of the service when the code is unknown', () => {
    expect(
      rejectionText({ code: 'codice-nuovo', limits: null, detail: 'Testo del servizio.' }),
    ).toBe('Testo del servizio.');
  });

  it('falls back to the text of the service when the service sends no code', () => {
    expect(rejectionText({ code: null, limits: null, detail: 'Testo del servizio.' })).toBe(
      'Testo del servizio.',
    );
  });

  it('falls back to the text of the service when a limit is missing or not a number', () => {
    expect(
      rejectionText({ code: 'outside-opening-hours', limits: null, detail: 'Fuori orario.' }),
    ).toBe('Fuori orario.');
    expect(
      rejectionText({
        code: 'duration-exceeded',
        limits: { maxDurationMinutes: 'quattro' },
        detail: 'Troppo lunga.',
      }),
    ).toBe('Troppo lunga.');
  });

  it('is null when there is neither a known code nor a text', () => {
    expect(rejectionText({ code: null, limits: null, detail: null })).toBeNull();
    expect(rejectionText({ code: 'codice-nuovo', limits: null, detail: '  ' })).toBeNull();
  });
});
