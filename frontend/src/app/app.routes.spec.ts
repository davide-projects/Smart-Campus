import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Title } from '@angular/platform-browser';
import { Router, TitleStrategy, provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { routes } from './app.routes';
import { AppTitleStrategy } from './services/app-title.strategy';
import { AuthService } from './services/auth.service';
import { BookingsService } from './services/bookings.service';
import { RoomsService } from './services/rooms.service';

@Component({ template: '' })
class EmptyPage {}

describe('app routes', () => {
  const isLoggedIn = signal(false);
  const isStaff = signal(false);
  let router: Router;

  beforeEach(() => {
    isLoggedIn.set(false);
    isStaff.set(false);
    TestBed.configureTestingModule({
      providers: [
        // La pagina di login è sostituita da una vuota: qui conta solo la navigazione.
        provideRouter([
          { path: 'login', component: EmptyPage },
          // Dove finisce chi non può aprire una pagina riservata.
          { path: 'rooms', component: EmptyPage },
          ...routes,
        ]),
        { provide: TitleStrategy, useClass: AppTitleStrategy },
        { provide: AuthService, useValue: { isLoggedIn, isStaff } },
        {
          provide: RoomsService,
          useValue: {
            listFormOptions: () => of({ buildings: [], equipment: [] }),
            getRoomById: () =>
              of({
                id: 'room-1',
                name: 'Aula A1',
                type: 'Classroom',
                buildingId: 'b-1',
                buildingName: 'Edificio A',
                buildingCode: 'A',
                floor: 0,
                capacity: 10,
                isBookable: true,
                equipment: [],
                openingHours: [],
              }),
          },
        },
        {
          provide: BookingsService,
          useValue: { getMine: () => of({ upcoming: [], past: [], cancelled: [] }) },
        },
      ],
    });
    router = TestBed.inject(Router);
  });

  it('opens the home page for everyone, with its title', async () => {
    await router.navigateByUrl('/');

    expect(router.url).toBe('/');
    expect(TestBed.inject(Title).getTitle()).toBe('Home · SmartCampus');
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

  it('sends a guest from the new room form to the login, remembering the page', async () => {
    await router.navigateByUrl('/staff/rooms/new');

    expect(router.url).toBe('/login?returnUrl=%2Fstaff%2Frooms%2Fnew');
  });

  it('keeps a student out of the room management pages', async () => {
    isLoggedIn.set(true);

    await router.navigateByUrl('/staff/rooms/new');
    expect(router.url).toBe('/rooms');

    await router.navigateByUrl('/staff/rooms/room-1/edit');
    expect(router.url).toBe('/rooms');
  });

  it('opens the room management pages for the staff, with their titles', async () => {
    isLoggedIn.set(true);
    isStaff.set(true);

    await router.navigateByUrl('/staff/rooms/new');
    expect(router.url).toBe('/staff/rooms/new');
    expect(TestBed.inject(Title).getTitle()).toBe('Nuovo spazio · SmartCampus');

    await router.navigateByUrl('/staff/rooms/room-1/edit');
    expect(router.url).toBe('/staff/rooms/room-1/edit');
    expect(TestBed.inject(Title).getTitle()).toBe('Modifica spazio · SmartCampus');
  });
});
