import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { AuthService, LOGIN_URL } from './auth.service';

// Aggiunge il token alle chiamate verso /api. Se il backend lo rifiuta (401), la sessione è chiusa
// e si torna al login. La richiesta di login è esclusa: lì un 401 vuol dire credenziali errate.
export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const auth = inject(AuthService);
  const token = auth.token();
  if (!token || !request.url.startsWith('/api/') || request.url === LOGIN_URL) {
    return next(request);
  }

  return next(request.clone({ setHeaders: { Authorization: `Bearer ${token}` } })).pipe(
    catchError((error: unknown) => {
      if (error instanceof HttpErrorResponse && error.status === 401) {
        auth.expireSession();
      }
      return throwError(() => error);
    }),
  );
};
