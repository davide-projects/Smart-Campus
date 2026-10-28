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

  it('copes with an empty or non-JSON body', () => {
    expect(problemOf(new HttpErrorResponse({ status: 500, error: 'boom' })).errors).toEqual({});
    expect(problemOf(new HttpErrorResponse({ status: 503 })).status).toBe(503);
  });

  it('reports status 0 for anything that is not an HTTP error', () => {
    expect(problemOf(new Error('rete'))).toEqual({ status: 0, title: null, detail: null, errors: {} });
  });
});
