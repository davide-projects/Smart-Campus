import { HarnessLoader } from '@angular/cdk/testing';
import { TestbedHarnessEnvironment } from '@angular/cdk/testing/testbed';
import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MatMenuHarness } from '@angular/material/menu/testing';
import { provideRouter } from '@angular/router';
import { UserProfile } from '../../../models/user.model';
import { AuthService } from '../../../services/auth.service';
import { NavLink, Navbar, visibleLinks } from './navbar';

// Dati sintetici di prova.
const STUDENT: UserProfile = { id: 'u1', username: 'student1', displayName: 'Mario Rossi', role: 'Student' };
const STAFF: UserProfile = { id: 'u2', username: 'staff1', displayName: 'Elena Conti', role: 'Staff' };

describe('Navbar', () => {
  const user = signal<UserProfile | null>(null);
  const logout = vi.fn();
  let fixture: ComponentFixture<Navbar>;
  let element: HTMLElement;
  let loader: HarnessLoader;

  async function render(): Promise<void> {
    await TestBed.configureTestingModule({
      imports: [Navbar],
      providers: [provideRouter([]), { provide: AuthService, useValue: { user, logout } }],
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

  it('has the menu button for mobile', async () => {
    await render();

    expect(element.querySelector('button[aria-label="Apri il menu"]')).not.toBeNull();
  });

  it('offers the login to a guest', async () => {
    await render();

    const login = [...element.querySelectorAll('a')].find((a) => a.textContent!.trim() === 'Accedi')!;
    expect(login.getAttribute('href')).toBe('/login');
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
