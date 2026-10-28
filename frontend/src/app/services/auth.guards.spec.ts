import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { authGuard, staffGuard } from './auth.guards';
import { AuthService } from './auth.service';

@Component({ template: '' })
class EmptyPage {}

describe('auth guards', () => {
  const isLoggedIn = signal(false);
  const isStaff = signal(false);
  let router: Router;

  beforeEach(() => {
    isLoggedIn.set(false);
    isStaff.set(false);
    TestBed.configureTestingModule({
      providers: [
        provideRouter([
          { path: 'login', component: EmptyPage },
          { path: 'rooms', component: EmptyPage },
          { path: 'my-bookings', component: EmptyPage, canActivate: [authGuard] },
          { path: 'staff', component: EmptyPage, canActivate: [staffGuard] },
        ]),
        { provide: AuthService, useValue: { isLoggedIn, isStaff } },
      ],
    });
    router = TestBed.inject(Router);
  });

  it('sends a guest to the login, remembering the requested page', async () => {
    await router.navigateByUrl('/my-bookings');

    expect(router.url).toBe('/login?returnUrl=%2Fmy-bookings');
  });

  it('opens the protected page for a logged in user', async () => {
    isLoggedIn.set(true);

    await router.navigateByUrl('/my-bookings');

    expect(router.url).toBe('/my-bookings');
  });

  it('sends a guest to the login from a staff page', async () => {
    await router.navigateByUrl('/staff');

    expect(router.url).toBe('/login?returnUrl=%2Fstaff');
  });

  it('keeps a student out of the staff pages', async () => {
    isLoggedIn.set(true);

    await router.navigateByUrl('/staff');

    expect(router.url).toBe('/rooms');
  });

  it('opens the staff pages for the staff', async () => {
    isLoggedIn.set(true);
    isStaff.set(true);

    await router.navigateByUrl('/staff');

    expect(router.url).toBe('/staff');
  });
});
