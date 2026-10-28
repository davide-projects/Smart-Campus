import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { Home } from './home';

describe('Home', () => {
  let element: HTMLElement;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Home],
      providers: [provideRouter([])],
    }).compileComponents();

    const fixture: ComponentFixture<Home> = TestBed.createComponent(Home);
    element = fixture.nativeElement;
    await fixture.whenStable();
  });

  it('shows the app name and the tagline', () => {
    expect(element.querySelector('h1')!.textContent).toBe('SmartCampus');
    expect(element.querySelector('.tagline')!.textContent).toBe('La tua università a portata di mano');
  });

  it('has a button with the calendar icon that opens the room search', () => {
    const button = element.querySelector<HTMLAnchorElement>('a[href="/rooms"]')!;

    expect(button.textContent).toContain('Prenota ora il tuo spazio');
    expect(button.querySelector('mat-icon')!.textContent).toContain('calendar_month');
  });

  it('has no other links: sign-in and reservations stay in the navbar', () => {
    expect(element.querySelectorAll('a')).toHaveLength(1);
  });
});
