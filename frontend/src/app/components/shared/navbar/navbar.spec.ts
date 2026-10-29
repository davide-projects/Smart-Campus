import { HarnessLoader } from '@angular/cdk/testing';
import { TestbedHarnessEnvironment } from '@angular/cdk/testing/testbed';
import { Component, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MatMenuHarness } from '@angular/material/menu/testing';
import { Router, provideRouter } from '@angular/router';
import { UserProfile } from '../../../models/user.model';
import { AuthService } from '../../../services/auth.service';
import { NavLink, Navbar, visibleLinks } from './navbar';

// Dati sintetici di prova.
const STUDENT: UserProfile = { id: 'u1', username: 'student1', displayName: 'Mario Rossi', role: 'Student' };
const STAFF: UserProfile = { id: 'u2', username: 'staff1', displayName: 'Elena Conti', role: 'Staff' };

@Component({ template: '' })
class EmptyPage {}

describe('Navbar', () => {
  const user = signal<UserProfile | null>(null);
  const logout = vi.fn();
  let fixture: ComponentFixture<Navbar>;
  let element: HTMLElement;
  let loader: HarnessLoader;

  async function render(): Promise<void> {
    await TestBed.configureTestingModule({
      imports: [Navbar],
      providers: [
        provideRouter([{ path: 'login', component: EmptyPage }]),
        { provide: AuthService, useValue: { user, logout } },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(Navbar);
    element = fixture.nativeElement;
    loader = TestbedHarnessEnvironment.loader(fixture);
    await fixture.whenStable();
  }

  beforeEach(() => {
    user.set(null);
    logout.mockReset();
  });

  it('shows the app name and the link to the room search', async () => {
    await render();

    expect(element.querySelector('.brand')!.textContent).toContain('SmartCampus');
    const link = element.querySelector<HTMLAnchorElement>('nav a')!;
    expect(link.textContent!.trim()).toBe('Cerca spazi');
    expect(link.getAttribute('href')).toBe('/rooms');
  });

  it('has the logo as a link to the home page, for guests and users alike', async () => {
    await render();
    const guestBrand = element.querySelector<HTMLAnchorElement>('a.brand')!;
    expect(guestBrand.getAttribute('href')).toBe('/');
    expect(guestBrand.textContent!.trim()).toBe('SmartCampus');
    expect(guestBrand.getAttribute('aria-label')).toContain('Vai alla pagina iniziale');

    user.set(STUDENT);
    fixture.detectChanges();
    expect(element.querySelector<HTMLAnchorElement>('a.brand')!.getAttribute('href')).toBe('/');
  });

  it('keeps the page links as they were: the logo is not one of them', async () => {
    user.set(STUDENT);
    await render();

    // Le voci di navigazione restano quelle di prima: il logo non ne è una.
    expect(navLinks().map((link) => link.text)).toEqual(['Cerca spazi', 'Spazi liberi', 'Le mie prenotazioni']);
  });

  function navLinks(): { text: string; href: string | null }[] {
    return [...element.querySelectorAll<HTMLAnchorElement>('nav a')].map((a) => ({
      text: a.textContent!.trim(),
      href: a.getAttribute('href'),
    }));
  }

  const MY_BOOKINGS = { text: 'Le mie prenotazioni', href: '/my-bookings' };

  it('shows the link to my reservations to a student', async () => {
    user.set(STUDENT);
    await render();

    expect(navLinks()).toContainEqual(MY_BOOKINGS);
  });

  it('shows the link to my reservations to the staff', async () => {
    user.set(STAFF);
    await render();

    expect(navLinks()).toContainEqual(MY_BOOKINGS);
  });

  it('hides the link to my reservations from a guest', async () => {
    await render();

    expect(navLinks()).not.toContainEqual(MY_BOOKINGS);
  });

  const AVAILABLE_ROOMS = { text: 'Spazi liberi', href: '/available-rooms' };

  it('shows the link to available rooms to a student', async () => {
    user.set(STUDENT);
    await render();

    expect(navLinks()).toContainEqual(AVAILABLE_ROOMS);
  });

  it('hides the link to available rooms from a guest', async () => {
    await render();

    expect(navLinks()).not.toContainEqual(AVAILABLE_ROOMS);
  });

  const NEW_ROOM = { text: 'Nuovo spazio', href: '/staff/rooms/new' };
  const RESERVATIONS_LINK = { text: 'Prenotazioni', href: '/staff/reservations' };

  it('shows the link to create a room to the staff only', async () => {
    user.set(STAFF);
    await render();

    expect(navLinks()).toContainEqual(NEW_ROOM);
  });

  it('hides the link to create a room from students and guests', async () => {
    user.set(STUDENT);
    await render();

    expect(navLinks()).not.toContainEqual(NEW_ROOM);
  });

  it('shows the link to reservations to the staff only', async () => {
    user.set(STAFF);
    await render();

    expect(navLinks()).toContainEqual(RESERVATIONS_LINK);
  });

  it('hides the link to reservations from students and guests', async () => {
    user.set(STUDENT);
    await render();

    expect(navLinks()).not.toContainEqual(RESERVATIONS_LINK);
  });

  it('has the link to my reservations in the mobile menu', async () => {
    user.set(STUDENT);
    await render();

    const menu = await loader.getHarness(
      MatMenuHarness.with({ selector: 'button[aria-label="Apri il menu"]' }),
    );
    await menu.open();

    const items = await Promise.all((await menu.getItems()).map((item) => item.getText()));
    expect(items).toEqual(['Cerca spazi', 'Spazi liberi', 'Le mie prenotazioni']);
  });

  it('has the menu button for mobile', async () => {
    await render();

    expect(element.querySelector('button[aria-label="Apri il menu"]')).not.toBeNull();
  });

  function loginLink(): HTMLAnchorElement | undefined {
    return [...element.querySelectorAll('a')].find((a) => a.textContent!.trim() === 'Accedi');
  }

  it('offers the login to a guest', async () => {
    await render();

    expect(loginLink()!.getAttribute('href')).toBe('/login');
  });

  it('hides the login button on the login page', async () => {
    await render();

    await TestBed.inject(Router).navigateByUrl('/login');
    await fixture.whenStable();

    expect(loginLink()).toBeUndefined();
  });

  it('shows name and role of the logged in user and lets them log out', async () => {
    user.set(STUDENT);
    await render();

    const menu = await loader.getHarness(MatMenuHarness.with({ selector: '.user-toggle' }));
    expect(await menu.getTriggerText()).toContain('Mario Rossi');
    await menu.open();
    const panel = document.querySelector('.user-info')!.textContent!;
    expect(panel).toContain('Mario Rossi');
    expect(panel).toContain('Studente');

    await menu.clickItem({ text: /Esci/ });

    expect(logout).toHaveBeenCalled();
  });
});

describe('visibleLinks', () => {
  const links: NavLink[] = [
    { path: '/rooms', label: 'Cerca spazi' },
    { path: '/my-bookings', label: 'Le mie prenotazioni', access: 'user' },
    { path: '/manage-rooms', label: 'Gestione spazi', access: 'staff' },
  ];

  it('shows only the public pages to a guest', () => {
    expect(visibleLinks(links, null).map((link) => link.path)).toEqual(['/rooms']);
  });

  it('hides the staff pages from a student', () => {
    expect(visibleLinks(links, STUDENT).map((link) => link.path)).toEqual(['/rooms', '/my-bookings']);
  });

  it('shows every page to the staff', () => {
    expect(visibleLinks(links, STAFF)).toEqual(links);
  });
});
