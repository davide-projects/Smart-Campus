import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { of, throwError } from 'rxjs';
import { vi } from 'vitest';

import { DeactivateRoomDialog } from './deactivate-room-dialog';
import { BookingsService } from '../../services/bookings.service';

describe('DeactivateRoomDialog', () => {
  let component: DeactivateRoomDialog;
  let fixture: ComponentFixture<DeactivateRoomDialog>;
  let mockBookingsService: { futureReservationsCount: ReturnType<typeof vi.fn> };
  let mockDialogRef: { close: ReturnType<typeof vi.fn> };

  beforeEach(async () => {
    mockBookingsService = {
      // Il servizio reale espone già il conteggio mappato (Observable<number>).
      futureReservationsCount: vi.fn().mockReturnValue(of(5)),
    };
    mockDialogRef = { close: vi.fn() };

    await TestBed.configureTestingModule({
      imports: [DeactivateRoomDialog],
      providers: [
        { provide: BookingsService, useValue: mockBookingsService },
        { provide: MAT_DIALOG_DATA, useValue: { roomId: 'room-1', roomName: 'Aula A1' } },
        { provide: MatDialogRef, useValue: mockDialogRef },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(DeactivateRoomDialog);
    component = fixture.componentInstance;
  });

  // Criterio della storia: il numero mostrato corrisponde a quello restituito dall'API.
  it('dovrebbe mostrare il numero di prenotazioni future restituito dall\'API', () => {
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Aula A1');
    expect(compiled.textContent).toContain('Prenotazioni future');
    expect(compiled.textContent).toContain('5');
    expect(mockBookingsService.futureReservationsCount).toHaveBeenCalledWith('room-1');
  });

  it('dovrebbe tenere il pulsante Dismetti disabilitato finché il conteggio non è pronto', () => {
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(component['canConfirm']()).toBe(true);

    const confirmButton = Array.from(compiled.querySelectorAll('button')).find((button) =>
      button.textContent?.includes('Dismetti'),
    );
    expect(confirmButton?.disabled).toBe(false);
  });

  it('dovrebbe mostrare un errore con la possibilità di riprovare se l\'API fallisce', () => {
    mockBookingsService.futureReservationsCount
      .mockReturnValueOnce(throwError(() => new Error('network')))
      .mockReturnValueOnce(of(2));

    fixture.detectChanges();

    // Primo tentativo fallito: messaggio di errore, nessun conteggio, pulsante disabilitato.
    const compiled = fixture.nativeElement as HTMLElement;
    expect(component['errorMessage']()).toBeTruthy();
    expect(component['count']()).toBeNull();
    expect(component['canConfirm']()).toBe(false);

    // Riprova: il conteggio appare e il pulsante si abilita.
    component['retry']();
    fixture.detectChanges();

    expect(component['errorMessage']()).toBeNull();
    expect(component['count']()).toBe(2);
    expect(component['canConfirm']()).toBe(true);
  });

  it('dovrebbe chiudere la finestra con true alla conferma e con false all\'annullamento', () => {
    fixture.detectChanges();

    component['confirm']();
    expect(mockDialogRef.close).toHaveBeenCalledWith(true);

    component['cancel']();
    expect(mockDialogRef.close).toHaveBeenCalledWith(false);
  });
});
