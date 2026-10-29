import { AvailabilitySlot, buildTimeline, formatDuration } from './availability.model';

const pad = (value: number) => String(value).padStart(2, '0');

// Minuti da mezzanotte → data-ora del 3 novembre 2026 in ora del campus (+01:00), come la invia l'API.
const at = (minutes: number) =>
  `2026-11-03T${pad(Math.floor(minutes / 60))}:${pad(minutes % 60)}:00+01:00`;

const hm = (hours: number, minutes = 0) => hours * 60 + minutes;

// Fasce da 30 minuti da `open` a `close`; `busy` sono gli intervalli occupati, in minuti da mezzanotte.
function slots(open: number, close: number, busy: [number, number][] = []): AvailabilitySlot[] {
  const result: AvailabilitySlot[] = [];
  for (let start = open; start < close; start += 30) {
    const isBusy = busy.some(([from, to]) => start >= from && start + 30 <= to);
    result.push({ start: at(start), end: at(start + 30), isFree: !isBusy });
  }
  return result;
}

describe('formatDuration', () => {
  it('shows minutes, hours or both', () => {
    expect(formatDuration(30)).toBe('30 min');
    expect(formatDuration(720)).toBe('12 h');
    expect(formatDuration(450)).toBe('7 h 30 min');
  });
});

describe('buildTimeline', () => {
  it('returns null without slots', () => {
    expect(buildTimeline([])).toBeNull();
  });

  it('merges consecutive slots with the same state into one block', () => {
    const timeline = buildTimeline(
      slots(hm(8), hm(20), [
        [hm(9), hm(10, 30)],
        [hm(12), hm(13)],
      ]),
    )!;

    expect(timeline.blocks.map((block) => [block.range, block.isFree])).toEqual([
      ['08:00–09:00', true],
      ['09:00–10:30', false],
      ['10:30–12:00', true],
      ['12:00–13:00', false],
      ['13:00–20:00', true],
    ]);
  });

  it('gives each block a tooltip and a text for screen readers', () => {
    const [free, busy] = buildTimeline(slots(hm(8), hm(9), [[hm(8, 30), hm(9)]]))!.blocks;

    expect(free.tooltip).toBe('Libera · 08:00–08:30');
    expect(free.srText).toBe('Libera dalle 08:00 alle 08:30');
    expect(busy.tooltip).toBe('Occupata · 08:30–09:00');
    expect(busy.srText).toBe('Occupata dalle 08:30 alle 09:00');
  });

  it('describes a fully free day with a single block', () => {
    const timeline = buildTimeline(slots(hm(8), hm(20)))!;

    expect(timeline.blocks).toHaveLength(1);
    expect(timeline.freeMinutes).toBe(720);
    expect(timeline.freeLabel).toBe('Libero 12 h');
    expect(timeline.totalLabel).toBe('su 12 h');
  });

  it('adds up the free time of a day with reservations', () => {
    const timeline = buildTimeline(
      slots(hm(8), hm(20), [
        [hm(9), hm(10, 30)],
        [hm(12), hm(13)],
      ]),
    )!;

    expect(timeline.totalMinutes).toBe(720);
    expect(timeline.freeMinutes).toBe(570);
    expect(timeline.freeLabel).toBe('Libero 9 h 30 min');
  });

  it('says there are no free slots when everything is taken', () => {
    const timeline = buildTimeline(slots(hm(8), hm(20), [[hm(8), hm(20)]]))!;

    expect(timeline.blocks).toHaveLength(1);
    expect(timeline.blocks[0].isFree).toBe(false);
    expect(timeline.freeLabel).toBe('Nessuna fascia libera');
  });

  it('keeps a lone free half hour as its own block', () => {
    const timeline = buildTimeline(
      slots(hm(8), hm(20), [
        [hm(8), hm(17, 30)],
        [hm(18), hm(20)],
      ]),
    )!;

    expect(timeline.blocks.map((block) => block.range)).toEqual([
      '08:00–17:30',
      '17:30–18:00',
      '18:00–20:00',
    ]);
    expect(timeline.freeLabel).toBe('Libero 30 min');
  });

  describe('labels inside the blocks', () => {
    // 08:00–20:00 con tre prenotazioni: 08:00–09:00 (1 h), 10:00–13:00 (3 h), 14:00–18:00 (4 h).
    const blocks = buildTimeline(
      slots(hm(8), hm(20), [
        [hm(8), hm(9)],
        [hm(10), hm(13)],
        [hm(14), hm(18)],
      ]),
    )!.blocks;
    const byRange = (range: string) => blocks.find((block) => block.range === range)!;

    it('does not show the time in a block that is too short', () => {
      // 1 ora su 12 = 8%: non ci sta né su una barra larga né su una stretta.
      expect(byRange('08:00–09:00').fitsWide).toBe(false);
      expect(byRange('08:00–09:00').fitsNarrow).toBe(false);
    });

    it('uses a lower threshold on wide bars than on narrow ones', () => {
      // 3 ore su 12 = 25%: ci sta su una barra larga ma non su una stretta.
      expect(byRange('10:00–13:00').fitsWide).toBe(true);
      expect(byRange('10:00–13:00').fitsNarrow).toBe(false);
      // 4 ore su 12 = 33%: ci sta su entrambe.
      expect(byRange('14:00–18:00').fitsWide).toBe(true);
      expect(byRange('14:00–18:00').fitsNarrow).toBe(true);
    });
  });

  describe('ticks', () => {
    it('shows the opening and closing time and every full hour in between', () => {
      const { ticks } = buildTimeline(slots(hm(8), hm(20)))!;

      expect(ticks.map((tick) => tick.label)).toEqual([
        '08:00',
        '09:00',
        '10:00',
        '11:00',
        '12:00',
        '13:00',
        '14:00',
        '15:00',
        '16:00',
        '17:00',
        '18:00',
        '19:00',
        '20:00',
      ]);
      expect(ticks[0]).toMatchObject({ percent: 0, edge: 'first' });
      expect(ticks[ticks.length - 1]).toMatchObject({ percent: 100, edge: 'last' });
    });

    it('places each tick at its position along the bar', () => {
      const { ticks } = buildTimeline(slots(hm(8), hm(20)))!;

      expect(ticks.find((tick) => tick.label === '11:00')!.percent).toBe(25);
      expect(ticks.find((tick) => tick.label === '14:00')!.percent).toBe(50);
    });

    it('keeps one hour in three on narrow bars', () => {
      const { ticks } = buildTimeline(slots(hm(8), hm(20)))!;

      expect(ticks.filter((tick) => tick.showOnNarrow).map((tick) => tick.label)).toEqual([
        '08:00',
        '11:00',
        '14:00',
        '17:00',
        '20:00',
      ]);
    });

    it('skips an hour that is too close to the opening or closing time', () => {
      const { ticks } = buildTimeline(slots(hm(8, 30), hm(11, 30)))!;

      // 09:00 dista 30 minuti da 08:30 e 11:00 dista 30 minuti da 11:30: restano solo gli estremi e le 10:00.
      expect(ticks.map((tick) => tick.label)).toEqual(['08:30', '10:00', '11:30']);
    });
  });
});
