import { inject } from '@angular/core';
import { CanActivateFn, Router, UrlTree } from '@angular/router';
import { AuthService } from './auth.service';

// Pagine riservate agli utenti autenticati: chi non ha fatto l'accesso va al login,
// che poi lo riporta alla pagina richiesta.
export const authGuard: CanActivateFn = (_route, state) =>
  inject(AuthService).isLoggedIn() || loginRedirect(state.url);

// Pagine riservate al personale: uno studente viene riportato alla ricerca degli spazi.
export const staffGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthService);
  if (!auth.isLoggedIn()) {
    return loginRedirect(state.url);
  }
  return auth.isStaff() || inject(Router).createUrlTree(['/rooms']);
};

function loginRedirect(returnUrl: string): UrlTree {
  return inject(Router).createUrlTree(['/login'], { queryParams: { returnUrl } });
}
