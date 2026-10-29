import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { HarnessLoader } from '@angular/cdk/testing';
import { TestbedHarnessEnvironment } from '@angular/cdk/testing/testbed';
import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MatButtonHarness } from '@angular/material/button/testing';
import { MATERIAL_ANIMATIONS } from '@angular/material/core';
import { MatDialog } from '@angular/material/dialog';
import { MatDialogHarness } from '@angular/material/dialog/testing';
import { MyReservationItem } from '../../../models/booking.model';
import { CancelBookingDialog } from './cancel-booking-dialog';

@Component({ template: '' })
class Host {}

// Dati sintetici di prova. A novembre Roma è a UTC+1: le 09:00Z sono le 10:00 del campus.
const RESERVATION: MyReservationItem = {
  id: 'p1',
  roomId: 'r1',
  roomName: 'Aula A1',
  buildingName: 'Edificio A',
  start: '2026-11-05T09:00:00+00:00',
  end: '2026-11-05T10:00:00+00:00',
  isGroup: false,
  organizerName: null,
  isOrganizer: true,
  checkedInAt: null,
};

const GENERIC = 'Non è stato possibile cancellare la prenotazione. Riprova più tardi.';

describe('CancelBookingDialog', () => {
  let fixture: ComponentFixture<Host>;
  let loader: HarnessLoader;
  let http: HttpTestingController;
  let dialog: MatDialog;
  let result: { value: boolean | undefined; closed: boolean };

  async function open(item: MyReservationItem = RESERVATION): Promise<MatDialogHarness> {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: MATERIAL_ANIMATIONS, useValue: { animationsDisabled: true } },
      ],
    });
    fixture = TestBed.createComponent(Host);
    loader = TestbedHarnessEnvironment.documentRootLoader(fixture);
    http = TestBed.inject(HttpTestingController);
    dialog = TestBed.inject(MatDialog);

    result = { value: undefined, closed: false };
    dialog
      .open<CancelBookingDialog, MyReservationItem, boolean>(CancelBookingDialog, { data: item })
      .afterClosed()
      .subscribe((value) => (result = { value, closed: true }));
    await fixture.whenStable();
    return loader.getHarness(MatDialogHarness);
  }

  async function button(text: string): Promise<MatButtonHarness> {
    return loader.getHarness(MatButtonHarness.with({ text }));
  }

  async function confirm(): Promise<void> {
    await (await button('Cancella prenotazione')).click();
    await fixture.whenStable();
  }

  async function buttonsDisabled(): Promise<boolean[]> {
    return Promise.all([
      (await button('Annulla')).isDisabled(),
      (await button('Cancella prenotazione')).isDisabled(),
    ]);
  }

  async function clickBackdrop(): Promise<void> {
    document.querySelector<HTMLElement>('.cdk-overlay-backdrop')!.click();
    await fixture.whenStable();
  }

  function alertText(): string | null {
    return document.querySelector('[role="alert"]')?.textContent!.trim() ?? null;
  }

  function isOpen(): boolean {
    return dialog.openDialogs.length > 0;
  }

  afterEach(() => {
    // Il dialog si chiude anche se il test fallisce, per non lasciarlo nel documento.
    dialog.closeAll();
    http.verify();
  });

  describe('summary', () => {
    it('shows title, room, building, day and time and the two buttons', async () => {
      const harness = await open();

      expect(await harness.getTitleText()).toBe('Cancellare la prenotazione?');
      const content = document.querySelector('mat-dialog-content')!;
      expect(content.querySelector('.room')!.textContent!.trim()).toBe('Aula A1');
      expect(content.querySelector('.building')!.textContent!.trim()).toBe('Edificio A');
      expect(content.querySelector('.time')!.textContent!.trim()).toBe(
        'giovedì 5 novembre 2026, 10:00–11:00',
      );
      const buttons = await harness.getAllHarnesses(MatButtonHarness);
      expect(await Promise.all(buttons.map((b) => b.getText()))).toEqual([
        'Annulla',
        'Cancella prenotazione',
      ]);
      expect(alertText()).toBeNull();
    });

    it('shows a dash when the building is not known', async () => {
      await open({ ...RESERVATION, buildingName: null });

      expect(document.querySelector('.building')!.textContent!.trim()).toBe('—');
    });
  });

  describe('closing without cancelling', () => {
    it('closes on Annulla without requests', async () => {
      await open();

      await (await button('Annulla')).click();
      await fixture.whenStable();

      expect(result).toEqual({ value: undefined, closed: true });
      http.expectNone('/api/bookings/p1');
    });

    it('closes on Esc without requests', async () => {
      const harness = await open();

      await harness.close();
      await fixture.whenStable();

      expect(result).toEqual({ value: undefined, closed: true });
      http.expectNone('/api/bookings/p1');
    });

    it('closes on a click on the backdrop without requests', async () => {
      await open();

      await clickBackdrop();

      expect(result).toEqual({ value: undefined, closed: true });
      http.expectNone('/api/bookings/p1');
    });
  });

  describe('request in progress', () => {
    it('sends a single DELETE even when confirmed twice', async () => {
      await open();

      await confirm();
      await confirm();

      const request = http.expectOne('/api/bookings/p1');
      expect(request.request.method).toBe('DELETE');
      request.flush(null, { status: 204, statusText: 'No Content' });
      await fixture.whenStable();

      expect(result).toEqual({ value: true, closed: true });
    });

    it('disables both buttons and ignores Esc, backdrop and Annulla while pending', async () => {
      const harness = await open();

      await confirm();

      expect(await buttonsDisabled()).toEqual([true, true]);
      await harness.close();
      await fixture.whenStable();
      expect(isOpen()).toBe(true);
      await clickBackdrop();
      expect(isOpen()).toBe(true);
      await (await button('Annulla')).click();
      await fixture.whenStable();
      expect(isOpen()).toBe(true);

      http.expectOne('/api/bookings/p1').flush(null, { status: 204, statusText: 'No Content' });
      await fixture.whenStable();
      expect(result).toEqual({ value: true, closed: true });
    });

    it('enables the buttons again after an error and closes on Esc', async () => {
      const harness = await open();

      await confirm();
      http.expectOne('/api/bookings/p1').flush(null, { status: 500, statusText: 'Server Error' });
      await fixture.whenStable();

      expect(await buttonsDisabled()).toEqual([false, false]);
      await harness.close();
      await fixture.whenStable();
      expect(result).toEqual({ value: undefined, closed: true });
    });
  });

  describe('errors', () => {
    const PROBLEMS = [
      { status: 403, detail: 'Non puoi cancellare questa prenotazione.' },
      { status: 404, detail: 'La prenotazione richiesta non esiste.' },
      {
        status: 422,
        detail: 'Non è possibile cancellare una prenotazione già iniziata o passata.',
      },
    ];

    for (const { status, detail } of PROBLEMS) {
      it(`shows the detail of a ${status} and stays open`, async () => {
        await open();

        await confirm();
        http
          .expectOne('/api/bookings/p1')
          .flush({ title: 'Errore', status, detail }, { status, statusText: 'Error' });
        await fixture.whenStable();

        expect(alertText()).toBe(detail);
        expect(isOpen()).toBe(true);
        expect(result.closed).toBe(false);
      });

      it(`shows the generic message when a ${status} has no detail`, async () => {
        await open();

        await confirm();
        http
          .expectOne('/api/bookings/p1')
          .flush({ title: 'Errore', status }, { status, statusText: 'Error' });
        await fixture.whenStable();

        expect(alertText()).toBe(GENERIC);
        expect(isOpen()).toBe(true);
      });
    }

    it('shows the generic message on a 500 even when it has a detail', async () => {
      await open();

      await confirm();
      http
        .expectOne('/api/bookings/p1')
        .flush(
          { title: 'Errore', status: 500, detail: 'Dettaglio interno del server.' },
          { status: 500, statusText: 'Server Error' },
        );
      await fixture.whenStable();

      expect(alertText()).toBe(GENERIC);
      expect(isOpen()).toBe(true);
    });

    it('shows the generic message on 500 and stays open', async () => {
      await open();

      await confirm();
      http.expectOne('/api/bookings/p1').flush(null, { status: 500, statusText: 'Server Error' });
      await fixture.whenStable();

      expect(alertText()).toBe(GENERIC);
      expect(isOpen()).toBe(true);
    });

    it('shows the generic message when the service does not respond', async () => {
      await open();

      await confirm();
      http.expectOne('/api/bookings/p1').error(new ProgressEvent('error'));
      await fixture.whenStable();

      expect(alertText()).toBe(GENERIC);
      expect(isOpen()).toBe(true);
    });

    it('closes without messages on 401', async () => {
      await open();

      await confirm();
      http.expectOne('/api/bookings/p1').flush(null, { status: 401, statusText: 'Unauthorized' });
      await fixture.whenStable();

      expect(result).toEqual({ value: undefined, closed: true });
      expect(alertText()).toBeNull();
    });
  });
});
