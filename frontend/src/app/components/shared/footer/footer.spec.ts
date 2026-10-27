import { TestBed } from '@angular/core/testing';
import { Footer } from './footer';

describe('Footer', () => {
  it('shows the current year, app name and project', async () => {
    await TestBed.configureTestingModule({ imports: [Footer] }).compileComponents();
    const fixture = TestBed.createComponent(Footer);
    await fixture.whenStable();

    const text = (fixture.nativeElement as HTMLElement).textContent!;
    expect(text).toContain(`© ${new Date().getFullYear()} SmartCampus · Project work`);
  });
});
