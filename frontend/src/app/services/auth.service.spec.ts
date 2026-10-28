import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { LoginResponse } from '../models/user.model';
import { AuthService } from './auth.service';

// Dati sintetici di prova.
function loginResponse(minutesToExpiry = 60): LoginResponse {
  return {
    accessToken: 'token-di-prova',
    tokenType: 'Bearer',
    expiresAt: new Date(Date.now() + minutesToExpiry * 60_000).toISOString(),
    user: { id: 'u1', username: 'student1', displayName: 'Mario Rossi', role: 'Student' },
  };
}

describe('AuthService', () => {
  let http: HttpTestingController;
  let router: Router;

  function setup(): AuthService {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });
    http = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
    vi.spyOn(router, 'navigate').mockResolvedValue(true);
    vi.spyOn(router, 'navigateByUrl').mockResolvedValue(true);
    return TestBed.inject(AuthService);
  }

  async function loginAs(auth: AuthService, response = loginResponse()): Promise<void> {
    const done = new Promise((resolve) => auth.login('student1', 'segreta').subscribe(resolve));
    http.expectOne('/api/identity/login').flush(response);
    await done;
  }

  afterEach(() => {
    http.verify();
    sessionStorage.clear();
    vi.useRealTimers();
  });

  it('sends the credentials and keeps the session after login', async () => {
    const auth = setup();
    const done = new Promise((resolve) => auth.login('student1', 'segreta').subscribe(resolve));

    const request = http.expectOne('/api/identity/login');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ username: 'student1', password: 'segreta' });
    request.flush(loginResponse());

    expect(await done).toEqual(loginResponse().user);
    expect(auth.isLoggedIn()).toBe(true);
    expect(auth.isStaff()).toBe(false);
    expect(auth.token()).toBe('token-di-prova');
  });

  it('restores a valid session after a page refresh', async () => {
    await loginAs(setup());
    TestBed.resetTestingModule();

    const auth = setup();

    expect(auth.user()?.displayName).toBe('Mario Rossi');
  });

  it('discards a saved session that has already expired', async () => {
    await loginAs(setup(), loginResponse(-1));
    TestBed.resetTestingModule();

    const auth = setup();

    expect(auth.isLoggedIn()).toBe(false);
    expect(sessionStorage.length).toBe(0);
  });

  it('forgets the session on logout and goes to the login page', async () => {
    const auth = setup();
    await loginAs(auth);

    auth.logout();

    expect(auth.isLoggedIn()).toBe(false);
    expect(auth.token()).toBeNull();
    expect(sessionStorage.length).toBe(0);
    expect(router.navigateByUrl).toHaveBeenCalledWith('/login');
  });

  it('ends the session when the token expires and asks to log in again', async () => {
    vi.useFakeTimers();
    const auth = setup();
    await loginAs(auth, loginResponse(1));

    vi.advanceTimersByTime(60_000);

    expect(auth.isLoggedIn()).toBe(false);
    expect(router.navigate).toHaveBeenCalledWith(['/login'], {
      queryParams: { reason: 'expired', returnUrl: '/' },
    });
  });
});
