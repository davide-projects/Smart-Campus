import { HttpErrorResponse } from '@angular/common/http';

// Errore di un servizio backend, in forma utile alla pagina.
// "errors" contiene i messaggi per campo di un ValidationProblem (400), con il nome del campo in minuscolo
// iniziale come nel modulo ("Name" diventa "name").
export interface ApiProblem {
  status: number;
  title: string | null;
  detail: string | null;
  errors: Record<string, string[]>;
}

export function problemOf(error: unknown): ApiProblem {
  if (!(error instanceof HttpErrorResponse)) {
    return { status: 0, title: null, detail: null, errors: {} };
  }

  const body = (error.error ?? {}) as {
    title?: unknown;
    detail?: unknown;
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
    errors,
  };
}
