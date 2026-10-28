import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, map, tap } from 'rxjs';
import { LoginResponse, UserProfile } from '../models/user.model';

export const LOGIN_URL = '/api/identity/login';

// La sessione resta in sessionStorage: sopravvive al refresh, ma non alla chiusura della scheda.
const STORAGE_KEY = 'smartcampus.session';

interface Session {
  accessToken: string;
  expiresAt: string;
  user: UserProfile;
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly session = signal<Session | null>(null);
  private expiryTimer?: ReturnType<typeof setTimeout>;

  readonly user = computed(() => this.session()?.user ?? null);
  readonly isLoggedIn = computed(() => this.session() !== null);
  readonly isStaff = computed(() => this.user()?.role === 'Staff');

  constructor() {
    const saved = readSavedSession();
    if (saved && !isExpired(saved)) {
      this.start(saved);
    } else {
      sessionStorage.removeItem(STORAGE_KEY);
    }
  }

  token(): string | null {
    return this.session()?.accessToken ?? null;
  }

  login(username: string, password: string): Observable<UserProfile> {
    return this.http.post<LoginResponse>(LOGIN_URL, { username, password }).pipe(
      tap(({ accessToken, expiresAt, user }) => {
        const session: Session = { accessToken, expiresAt, user };
        sessionStorage.setItem(STORAGE_KEY, JSON.stringify(session));
        this.start(session);
      }),
      map(({ user }) => user),
    );
  }

  logout(): void {
    this.clear();
    void this.router.navigateByUrl('/login');
  }

  // Token scaduto o rifiutato dal backend: si torna al login con l'avviso,
  // ricordando la pagina in cui si era.
  expireSession(): void {
    if (!this.session()) {
      return;
    }
    const returnUrl = this.router.url;
    this.clear();
    void this.router.navigate(['/login'], { queryParams: { reason: 'expired', returnUrl } });
  }

  private start(session: Session): void {
    this.session.set(session);
    clearTimeout(this.expiryTimer);
    this.expiryTimer = setTimeout(
      () => this.expireSession(),
      Math.max(0, Date.parse(session.expiresAt) - Date.now()),
    );
  }

  private clear(): void {
    clearTimeout(this.expiryTimer);
    sessionStorage.removeItem(STORAGE_KEY);
    this.session.set(null);
  }
}

function readSavedSession(): Session | null {
  try {
    return JSON.parse(sessionStorage.getItem(STORAGE_KEY) ?? 'null') as Session | null;
  } catch {
    return null;
  }
}

function isExpired(session: Session): boolean {
  return Date.parse(session.expiresAt) <= Date.now();
}
