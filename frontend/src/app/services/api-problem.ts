import { HttpErrorResponse } from '@angular/common/http';
import { FreeRange } from '../models/booking.model';

// Errore di un servizio backend, in forma utile alla pagina.
// "errors" contiene i messaggi per campo di un ValidationProblem (400), con il nome del campo in minuscolo
// iniziale come nel modulo ("Name" diventa "name").
// "code", "limits" e "freeSlots" sono dei rifiuti di Bookings (docs/handoff-architettura.md, 6.2):
// il codice di motivo, i limiti da rispettare (per esempio opensAt e closesAt) e, per lo spazio
// occupato, le fasce libere dello stesso giorno. Sono null quando il servizio non li invia; un
// elenco di fasce vuoto resta vuoto.
export interface ApiProblem {
  status: number;
  title: string | null;
  detail: string | null;
  code: string | null;
  limits: Record<string, unknown> | null;
  freeSlots: FreeRange[] | null;
  errors: Record<string, string[]>;
}

export function problemOf(error: unknown): ApiProblem {
  if (!(error instanceof HttpErrorResponse)) {
    return {
      status: 0,
      title: null,
      detail: null,
      code: null,
      limits: null,
      freeSlots: null,
      errors: {},
    };
  }

  const body = (error.error ?? {}) as {
    title?: unknown;
    detail?: unknown;
    code?: unknown;
    limits?: unknown;
    freeSlots?: unknown;
    errors?: Record<string, unknown>;
  };

  const errors: Record<string, string[]> = {};
  for (const [field, messages] of Object.entries(body.errors ?? {})) {
    if (Array.isArray(messages)) {
      errors[field.charAt(0).toLowerCase() + field.slice(1)] = messages.filter(
        (message): message is string => typeof message === 'string',
      );
    }
  }

  return {
    status: error.status,
    title: typeof body.title === 'string' ? body.title : null,
    detail: typeof body.detail === 'string' ? body.detail : null,
    code: typeof body.code === 'string' && body.code !== '' ? body.code : null,
    limits: limitsOf(body.limits),
    freeSlots: freeSlotsOf(body.freeSlots),
    errors,
  };
}

// I limiti da rispettare sono un oggetto nome-valore; ogni altra forma si ignora.
function limitsOf(value: unknown): Record<string, unknown> | null {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
    ? (value as Record<string, unknown>)
    : null;
}

// Le fasce libere hanno inizio e fine come testo; quelle malformate si scartano. Un elenco vuoto è
// un'informazione («nessuna fascia libera quel giorno») e non diventa null.
function freeSlotsOf(value: unknown): FreeRange[] | null {
  if (!Array.isArray(value)) {
    return null;
  }
  return value
    .filter(
      (slot): slot is FreeRange =>
        typeof slot === 'object' &&
        slot !== null &&
        typeof (slot as FreeRange).start === 'string' &&
        typeof (slot as FreeRange).end === 'string',
    )
    .map((slot) => ({ start: slot.start, end: slot.end }));
}
