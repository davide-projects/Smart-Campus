import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { Navbar } from './navbar';

describe('Navbar', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Navbar],
      providers: [provideRouter([])],
    }).compileComponents();
  });

  it('mostra il nome dell\'app e il link alla ricerca degli spazi', async () => {
    const fixture = TestBed.createComponent(Navbar);
    await fixture.whenStable();
    const element = fixture.nativeElement as HTMLElement;

    expect(element.querySelector('.brand')!.textContent).toContain('SmartCampus');
    const link = element.querySelector<HTMLAnchorElement>('nav a')!;
    expect(link.textContent!.trim()).toBe('Cerca spazi');
    expect(link.getAttribute('href')).toBe('/rooms');
  });

  it('ha il pulsante del menu per mobile', async () => {
    const fixture = TestBed.createComponent(Navbar);
    await fixture.whenStable();
    const element = fixture.nativeElement as HTMLElement;

    expect(element.querySelector('button[aria-label="Apri il menu"]')).not.toBeNull();
  });
});
