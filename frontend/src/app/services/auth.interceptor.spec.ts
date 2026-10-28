import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { authInterceptor } from './auth.interceptor';
import { AuthService } from './auth.service';

describe('authInterceptor', () => {
  let client: HttpClient;
  let http: HttpTestingController;
  let auth: { token: ReturnType<typeof vi.fn<() => string | null>>; expireSession: ReturnType<typeof vi.fn> };

  beforeEach(() => {
    auth = { token: vi.fn(() => 'token-di-prova'), expireSession: vi.fn() };
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        { provide: AuthService, useValue: auth },
      ],
    });
    client = TestBed.inject(HttpClient);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('adds the token to the API calls', () => {
    client.get('/api/rooms').subscribe();

    expect(http.expectOne('/api/rooms').request.headers.get('Authorization')).toBe('Bearer token-di-prova');
  });

  it('does not add any header when the user is not logged in', () => {
    auth.token.mockReturnValue(null);
    client.get('/api/rooms').subscribe();

    expect(http.expectOne('/api/rooms').request.headers.has('Authorization')).toBe(false);
  });

  it('does not send the token outside the API or to the login', () => {
    client.get('/assets/logo.svg').subscribe();
    client.post('/api/identity/login', {}).subscribe();

    expect(http.expectOne('/assets/logo.svg').request.headers.has('Authorization')).toBe(false);
    expect(http.expectOne('/api/identity/login').request.headers.has('Authorization')).toBe(false);
  });

  it('ends the session when the backend rejects the token', () => {
    client.get('/api/rooms').subscribe({ error: () => undefined });

    http.expectOne('/api/rooms').flush(null, { status: 401, statusText: 'Unauthorized' });

    expect(auth.expireSession).toHaveBeenCalled();
  });

  it('keeps the session on other errors', () => {
    client.get('/api/rooms').subscribe({ error: () => undefined });

    http.expectOne('/api/rooms').flush(null, { status: 403, statusText: 'Forbidden' });

    expect(auth.expireSession).not.toHaveBeenCalled();
  });
});
