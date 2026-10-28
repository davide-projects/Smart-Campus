import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Title } from '@angular/platform-browser';
import { Router, TitleStrategy, provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { routes } from './app.routes';
import { AppTitleStrategy } from './services/app-title.strategy';
import { AuthService } from './services/auth.service';
import { BookingsService } from './services/bookings.service';

@Component({ template: '' })
class EmptyPage {}

describe('app routes', () => {
  const isLoggedIn = signal(false);
  let router: Router;

  beforeEach(() => {
    isLoggedIn.set(false);
    TestBed.configureTestingModule({
      providers: [
        // La pagina di login è sostituita da una vuota: qui conta solo la navigazione.
        provideRouter([{ path: 'login', component: EmptyPage }, ...routes]),
        { provide: TitleStrategy, useClass: AppTitleStrategy },
        { provide: AuthService, useValue: { isLoggedIn } },
        {
          provide: BookingsService,
          useValue: { getMine: () => of({ upcoming: [], past: [], cancelled: [] }) },
        },
      ],
    });
    router = TestBed.inject(Router);
  });

  it('sends a guest from my reservations to the login, remembering the page', async () => {
    await router.navigateByUrl('/my-bookings');

    expect(router.url).toBe('/login?returnUrl=%2Fmy-bookings');
  });

  it('opens my reservations with its title for a logged in user', async () => {
    isLoggedIn.set(true);

    await router.navigateByUrl('/my-bookings');

    expect(router.url).toBe('/my-bookings');
    expect(TestBed.inject(Title).getTitle()).toBe('Le mie prenotazioni · SmartCampus');
  });
});
