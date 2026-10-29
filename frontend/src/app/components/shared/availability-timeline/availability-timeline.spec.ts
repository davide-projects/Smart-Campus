import { ComponentFixture, TestBed } from '@angular/core/testing';
import { AvailabilitySlot } from '../../../models/availability.model';
import { AvailabilityTimeline } from './availability-timeline';

const pad = (value: number) => String(value).padStart(2, '0');
const at = (hours: number, minutes = 0) => `2026-11-03T${pad(hours)}:${pad(minutes)}:00+01:00`;

// 08:00–10:00 in fasce da 30 minuti: libera, libera, occupata, occupata.
const slots: AvailabilitySlot[] = [
  { start: at(8), end: at(8, 30), isFree: true },
  { start: at(8, 30), end: at(9), isFree: true },
  { start: at(9), end: at(9, 30), isFree: false },
  { start: at(9, 30), end: at(10), isFree: false },
];

describe('AvailabilityTimeline', () => {
  let fixture: ComponentFixture<AvailabilityTimeline>;
  let element: HTMLElement;

  function render(input: AvailabilitySlot[]): void {
    fixture = TestBed.createComponent(AvailabilityTimeline);
    fixture.componentRef.setInput('slots', input);
    fixture.detectChanges();
    element = fixture.nativeElement as HTMLElement;
  }

  beforeEach(() => TestBed.configureTestingModule({ imports: [AvailabilityTimeline] }));

  it('draws one block for each stretch of the same state', () => {
    render(slots);

    const blocks = [...element.querySelectorAll('.block')];

    expect(blocks).toHaveLength(2);
    expect(blocks[0].classList).toContain('free');
    expect(blocks[1].classList).toContain('occupied');
  });

  it('gives the screen readers the state and the time of each block', () => {
    render(slots);

    const texts = [...element.querySelectorAll('.block .sr-only')].map((node) =>
      node.textContent!.trim(),
    );

    expect(texts).toEqual(['Libera dalle 08:00 alle 09:00', 'Occupata dalle 09:00 alle 10:00']);
  });

  it('lets the blocks be reached with the keyboard', () => {
    render(slots);

    for (const block of element.querySelectorAll('.block')) {
      expect(block.getAttribute('tabindex')).toBe('0');
    }
  });

  it('shows the summary, the legend and the times under the bar', () => {
    render(slots);

    expect(element.querySelector('.summary')!.textContent!.replace(/\s+/g, ' ').trim()).toBe(
      'Libero 1 h su 2 h',
    );
    expect(element.querySelector('.legend')!.textContent).toContain('Libera');
    expect(element.querySelector('.legend')!.textContent).toContain('Occupata');

    const ticks = [...element.querySelectorAll('.tick')].map((tick) => tick.textContent!.trim());
    expect(ticks).toEqual(['08:00', '09:00', '10:00']);
  });

  it('does not show who made the reservations', () => {
    render(slots);

    expect(element.textContent).not.toMatch(/organizer|Mario|Anna/i);
  });

  it('draws nothing without slots', () => {
    render([]);

    expect(element.querySelector('.bar')).toBeNull();
    expect(element.querySelector('.summary')).toBeNull();
  });
});
