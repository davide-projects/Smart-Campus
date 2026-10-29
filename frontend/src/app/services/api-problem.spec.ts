import { HttpErrorResponse } from '@angular/common/http';
import { problemOf } from './api-problem';

describe('problemOf', () => {
  it('keeps the field messages of a validation problem, with the field name in lower camel case', () => {
    const error = new HttpErrorResponse({
      status: 400,
      error: {
        title: 'One or more validation errors occurred.',
        errors: {
          Capacity: ['La capienza deve essere maggiore di zero.'],
          OpeningHours: ["L'orario di chiusura deve essere successivo all'apertura."],
        },
      },
    });

    const problem = problemOf(error);

    expect(problem.status).toBe(400);
    expect(problem.errors['capacity']).toEqual(['La capienza deve essere maggiore di zero.']);
    expect(problem.errors['openingHours']).toEqual([
      "L'orario di chiusura deve essere successivo all'apertura.",
    ]);
  });

  it('reads title and detail of a conflict', () => {
    const error = new HttpErrorResponse({
      status: 409,
      error: { title: 'Nome spazio già utilizzato', detail: 'Esiste già uno spazio con questo nome.' },
    });

    expect(problemOf(error)).toMatchObject({
      status: 409,
      title: 'Nome spazio già utilizzato',
      detail: 'Esiste già uno spazio con questo nome.',
      errors: {},
    });
  });

  it('reads the reason code and the limits of a rejection', () => {
    const error = new HttpErrorResponse({
      status: 422,
      error: {
        title: 'Fuori orario',
        detail: 'Lo spazio è aperto dalle 08:00 alle 20:00.',
        code: 'outside-opening-hours',
        limits: { opensAt: '08:00', closesAt: '20:00' },
      },
    });

    expect(problemOf(error)).toMatchObject({
      status: 422,
      code: 'outside-opening-hours',
      limits: { opensAt: '08:00', closesAt: '20:00' },
      freeSlots: null,
    });
  });

  it('reads the free slots of a room conflict', () => {
    const freeSlots = [
      { start: '2026-11-05T08:00:00+01:00', end: '2026-11-05T10:00:00+01:00' },
      { start: '2026-11-05T11:00:00+01:00', end: '2026-11-05T20:00:00+01:00' },
    ];
    const error = new HttpErrorResponse({
      status: 409,
      error: {
        title: 'Spazio già prenotato',
        detail: 'Lo spazio è già prenotato in questa fascia.',
        code: 'room-occupied',
        freeSlots,
      },
    });

    expect(problemOf(error)).toMatchObject({ code: 'room-occupied', limits: null, freeSlots });
  });

  it('keeps an empty list of free slots empty instead of turning it into null', () => {
    const error = new HttpErrorResponse({
      status: 409,
      error: { code: 'room-occupied', freeSlots: [] },
    });

    expect(problemOf(error).freeSlots).toEqual([]);
  });

  it('ignores the code, the limits and the free slots that are not in the expected shape', () => {
    const error = new HttpErrorResponse({
      status: 409,
      error: {
        code: '',
        limits: ['non', 'un', 'oggetto'],
        freeSlots: [{ start: '2026-11-05T08:00:00+01:00' }, 'testo', null],
      },
    });

    expect(problemOf(error)).toMatchObject({ code: null, limits: null, freeSlots: [] });
  });

  it('has no code, limits or free slots when the service does not send them', () => {
    const error = new HttpErrorResponse({ status: 409, error: { title: 'Conflitto' } });

    expect(problemOf(error)).toMatchObject({ code: null, limits: null, freeSlots: null });
  });

  it('copes with an empty or non-JSON body', () => {
    expect(problemOf(new HttpErrorResponse({ status: 500, error: 'boom' })).errors).toEqual({});
    expect(problemOf(new HttpErrorResponse({ status: 503 })).status).toBe(503);
  });

  it('reports status 0 for anything that is not an HTTP error', () => {
    expect(problemOf(new Error('rete'))).toEqual({
      status: 0,
      title: null,
      detail: null,
      code: null,
      limits: null,
      freeSlots: null,
      errors: {},
    });
  });
});
