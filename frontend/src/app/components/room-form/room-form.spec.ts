import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormControl, FormGroup } from '@angular/forms';
import { ActivatedRoute, Router, provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { vi } from 'vitest';
import { RoomDetail, RoomWriteRequest } from '../../models/room.model';
import { RoomsService } from '../../services/rooms.service';
import { MESSAGES, RoomForm, openingDayValidator, toApiTime } from './room-form';

// Dati sintetici di prova.
const BUILDINGS = [
  { id: 'b-1', name: 'Edificio A' },
  { id: 'b-2', name: 'Edificio B' },
];

const EQUIPMENT = [
  { id: 'e-2', name: 'Lavagna' },
  { id: 'e-1', name: 'Proiettore' },
  { id: 'e-3', name: 'Schermo' },
];

const EXISTING: RoomDetail = {
  id: 'room-1',
  name: 'Aula A1',
  type: 'Classroom',
  buildingId: 'b-1',
  buildingName: 'Edificio A',
  buildingCode: 'A',
  floor: 2,
  capacity: 30,
  isBookable: true,
  equipment: [
    { id: 'e-1', name: 'Proiettore' },
    { id: 'e-2', name: 'Lavagna' },
  ],
  openingHours: [
    { dayOfWeek: 'Monday', opensAt: '08:00:00', closesAt: '18:30:00', isClosed: false },
    { dayOfWeek: 'Tuesday', opensAt: null, closesAt: null, isClosed: true },
    { dayOfWeek: 'Wednesday', opensAt: '09:00:00', closesAt: '12:00:00', isClosed: false },
  ],
};

function badRequest(errors: Record<string, string[]>): HttpErrorResponse {
  return new HttpErrorResponse({ status: 400, error: { errors } });
}

describe('RoomForm', () => {
  const rooms = {
    listFormOptions: vi.fn(),
    getRoomById: vi.fn(),
    create: vi.fn(),
    update: vi.fn(),
  };
  let fixture: ComponentFixture<RoomForm>;
  let component: RoomForm;
  let element: HTMLElement;
  const navigate = vi.fn();

  // La pagina si apre sullo stesso componente per «nuovo» (senza id) e per «modifica» (con id).
  function render(id: string | null = null): void {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: RoomsService, useValue: rooms },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => id } } } },
      ],
    });
    TestBed.inject(Router).navigate = navigate;
    fixture = TestBed.createComponent(RoomForm);
    component = fixture.componentInstance;
    element = fixture.nativeElement;
    fixture.detectChanges();
  }

  // Compila i campi obbligatori con valori validi.
  function fillValid(): void {
    component['form'].patchValue({
      name: '  Aula C1  ',
      buildingId: 'b-2',
      type: 'Laboratory',
      floor: -1,
      capacity: 24,
      isBookable: false,
    });
    component['form'].controls.days.at(0).patchValue({ open: true, opensAt: '08:30', closesAt: '19:00' });
  }

  // Invia il modulo come farebbe il pulsante.
  function submit(): void {
    element.querySelector('form')!.dispatchEvent(new Event('submit'));
    // Due passate: la prima mostra gli errori, la seconda lascia che Material aggiorni i messaggi.
    fixture.detectChanges();
    fixture.detectChanges();
  }

  beforeEach(() => {
    navigate.mockReset().mockResolvedValue(true);
    rooms.listFormOptions
      .mockReset()
      .mockReturnValue(of({ buildings: BUILDINGS, equipment: EQUIPMENT }));
    rooms.getRoomById.mockReset().mockReturnValue(of(EXISTING));
    rooms.create.mockReset().mockReturnValue(of({ id: 'new-room' }));
    rooms.update.mockReset().mockReturnValue(of(undefined));
  });

  describe('new room', () => {
    beforeEach(() => render());

    it('opens an empty form with the new-room heading and the buildings and equipment to choose from', () => {
      expect(element.querySelector('h1')!.textContent).toContain('Nuovo spazio');
      expect(rooms.getRoomById).not.toHaveBeenCalled();
      expect(component['buildings']()).toEqual(BUILDINGS);
      expect(component['equipment']()).toEqual(EQUIPMENT);
      expect(component['form'].getRawValue().equipmentIds).toEqual([]);
      expect(component['form'].getRawValue().name).toBe('');
      expect(element.textContent).toContain('Crea lo spazio');
    });

    it('does not send an empty form and tells which fields are missing', () => {
      submit();

      expect(rooms.create).not.toHaveBeenCalled();
      expect(element.textContent).toContain(MESSAGES.nameRequired);
      expect(element.textContent).toContain(MESSAGES.buildingRequired);
      expect(element.textContent).toContain(MESSAGES.typeRequired);
      expect(element.textContent).toContain(MESSAGES.capacityRequired);
    });

    it('rejects a capacity of zero or less with a clear message', () => {
      fillValid();
      component['form'].controls.capacity.setValue(0);
      submit();

      expect(rooms.create).not.toHaveBeenCalled();
      expect(element.textContent).toContain(MESSAGES.capacityInvalid);
    });

    it('rejects a name made only of spaces', () => {
      fillValid();
      component['form'].controls.name.setValue('   ');
      submit();

      expect(rooms.create).not.toHaveBeenCalled();
      expect(element.textContent).toContain(MESSAGES.nameRequired);
    });

    it('rejects a name longer than 100 characters', () => {
      fillValid();
      component['form'].controls.name.setValue('a'.repeat(101));
      submit();

      expect(rooms.create).not.toHaveBeenCalled();
      expect(element.textContent).toContain(MESSAGES.nameTooLong);
    });

    it('rejects an opening day that closes before it opens', () => {
      fillValid();
      component['form'].controls.days.at(0).patchValue({ opensAt: '18:00', closesAt: '09:00' });
      submit();

      expect(rooms.create).not.toHaveBeenCalled();
      expect(element.textContent).toContain(MESSAGES.closesBeforeOpens);
    });

    it('creates the room, sending only the open days, then opens its page', () => {
      fillValid();
      submit();

      const expected: RoomWriteRequest = {
        buildingId: 'b-2',
        name: 'Aula C1',
        type: 'Laboratory',
        capacity: 24,
        floor: -1,
        isBookable: false,
        equipmentIds: [],
        openingHours: [{ dayOfWeek: 'Monday', opensAt: '08:30:00', closesAt: '19:00:00' }],
      };
      expect(rooms.create).toHaveBeenCalledWith(expected);
      expect(navigate).toHaveBeenCalledWith(['/rooms', 'new-room']);
    });

    it('creates the room with the chosen equipment', () => {
      fillValid();
      component['form'].controls.equipmentIds.setValue(['e-1', 'e-3']);
      submit();

      expect(rooms.create.mock.calls[0][0].equipmentIds).toEqual(['e-1', 'e-3']);
    });

    it('creates a room with no opening days when none is ticked', () => {
      fillValid();
      component['form'].controls.days.at(0).patchValue({ open: false });
      submit();

      expect(rooms.create.mock.calls[0][0].openingHours).toEqual([]);
    });

    it('shows the backend validation messages next to the fields', () => {
      fillValid();
      rooms.create.mockReturnValue(
        throwError(() =>
          badRequest({
            Capacity: ['La capienza deve essere maggiore di zero.'],
            OpeningHours: ["L'orario di chiusura deve essere successivo all'apertura."],
          }),
        ),
      );
      submit();

      expect(navigate).not.toHaveBeenCalled();
      expect(element.textContent).toContain('La capienza deve essere maggiore di zero.');
      expect(element.textContent).toContain("L'orario di chiusura deve essere successivo all'apertura.");
    });

    it('lets the user retry after a rejection', () => {
      fillValid();
      rooms.create.mockReturnValueOnce(
        throwError(() => badRequest({ Capacity: ['La capienza deve essere maggiore di zero.'] })),
      );
      submit();
      component['form'].controls.capacity.setValue(40);
      submit();

      expect(rooms.create).toHaveBeenCalledTimes(2);
      expect(navigate).toHaveBeenCalledWith(['/rooms', 'new-room']);
    });

    it('tells that the name is already taken in that building (409)', () => {
      fillValid();
      rooms.create.mockReturnValue(
        throwError(
          () =>
            new HttpErrorResponse({
              status: 409,
              error: { detail: "Esiste già uno spazio con questo nome nell'edificio indicato." },
            }),
        ),
      );
      submit();

      expect(element.textContent).toContain(
        "Esiste già uno spazio con questo nome nell'edificio indicato.",
      );
      expect(navigate).not.toHaveBeenCalled();
    });

    it('shows a generic message when the save fails for another reason', () => {
      fillValid();
      rooms.create.mockReturnValue(throwError(() => new HttpErrorResponse({ status: 500 })));
      submit();

      expect(element.textContent).toContain(MESSAGES.generic);
      expect(component['saving']()).toBe(false);
    });

    it('explains a refusal for missing permissions (403)', () => {
      fillValid();
      rooms.create.mockReturnValue(throwError(() => new HttpErrorResponse({ status: 403 })));
      submit();

      expect(element.textContent).toContain(MESSAGES.forbidden);
    });
  });

  describe('existing room', () => {
    beforeEach(() => render('room-1'));

    it('fills the form with the current data', () => {
      expect(element.querySelector('h1')!.textContent).toContain('Modifica spazio');
      expect(rooms.getRoomById).toHaveBeenCalledWith('room-1');
      expect(component['form'].getRawValue()).toMatchObject({
        name: 'Aula A1',
        buildingId: 'b-1',
        type: 'Classroom',
        floor: 2,
        capacity: 30,
        isBookable: true,
      });
      const days = component['form'].getRawValue().days;
      expect(days[0]).toEqual({ open: true, opensAt: '08:00', closesAt: '18:30' });
      expect(days[1].open).toBe(false);
      expect(days[2]).toEqual({ open: true, opensAt: '09:00', closesAt: '12:00' });
      expect(days[6].open).toBe(false);
      expect(component['form'].getRawValue().equipmentIds).toEqual(['e-1', 'e-2']);
      expect(element.textContent).toContain('Salva le modifiche');
    });

    it('offers the equipment of the room even when the search did not return it', () => {
      rooms.listFormOptions.mockReturnValue(of({ buildings: BUILDINGS, equipment: [] }));
      TestBed.resetTestingModule();
      render('room-1');

      expect(component['equipment']()).toEqual(EXISTING.equipment);
    });

    it('saves the changes keeping the current equipment', () => {
      component['form'].controls.capacity.setValue(45);
      submit();

      expect(rooms.update).toHaveBeenCalledWith('room-1', {
        buildingId: 'b-1',
        name: 'Aula A1',
        type: 'Classroom',
        capacity: 45,
        floor: 2,
        isBookable: true,
        equipmentIds: ['e-1', 'e-2'],
        openingHours: [
          { dayOfWeek: 'Monday', opensAt: '08:00:00', closesAt: '18:30:00' },
          { dayOfWeek: 'Wednesday', opensAt: '09:00:00', closesAt: '12:00:00' },
        ],
      });
      expect(rooms.create).not.toHaveBeenCalled();
      expect(navigate).toHaveBeenCalledWith(['/rooms', 'room-1']);
    });

    it('saves a changed selection of equipment, also when emptied', () => {
      component['form'].controls.equipmentIds.setValue(['e-3']);
      submit();
      expect(rooms.update.mock.calls[0][1].equipmentIds).toEqual(['e-3']);

      component['form'].controls.equipmentIds.setValue([]);
      submit();
      expect(rooms.update.mock.calls[1][1].equipmentIds).toEqual([]);
    });

    it('says so when the room was removed in the meantime (404 on save)', () => {
      rooms.update.mockReturnValue(throwError(() => new HttpErrorResponse({ status: 404 })));
      submit();

      expect(element.textContent).toContain(MESSAGES.notFound);
    });
  });

  describe('loading problems', () => {
    it('shows "not found" when the room to edit does not exist', () => {
      rooms.getRoomById.mockReturnValue(throwError(() => new HttpErrorResponse({ status: 404 })));
      render('missing');

      expect(element.textContent).toContain('Spazio non trovato');
      expect(element.querySelector('form')).toBeNull();
    });

    it('shows an error when the data cannot be loaded', () => {
      rooms.listFormOptions.mockReturnValue(throwError(() => new HttpErrorResponse({ status: 500 })));
      render();

      expect(element.textContent).toContain(MESSAGES.loadFailed);
      expect(element.querySelector('form')).toBeNull();
    });
  });
});

describe('openingDayValidator', () => {
  const day = (open: boolean, opensAt: string, closesAt: string) =>
    new FormGroup({
      open: new FormControl(open),
      opensAt: new FormControl(opensAt),
      closesAt: new FormControl(closesAt),
    });

  it('ignores a closed day, whatever its times', () => {
    expect(openingDayValidator(day(false, '20:00', '08:00'))).toBeNull();
  });

  it('accepts an open day that closes after it opens', () => {
    expect(openingDayValidator(day(true, '08:00', '20:00'))).toBeNull();
  });

  it('requires both times on an open day', () => {
    expect(openingDayValidator(day(true, '', '20:00'))).toEqual({ timeRequired: true });
    expect(openingDayValidator(day(true, '08:00', ''))).toEqual({ timeRequired: true });
  });

  it('rejects a closing time equal to or before the opening time', () => {
    expect(openingDayValidator(day(true, '08:00', '08:00'))).toEqual({ closesBeforeOpens: true });
    expect(openingDayValidator(day(true, '18:00', '09:00'))).toEqual({ closesBeforeOpens: true });
  });
});

describe('toApiTime', () => {
  it('adds the seconds Rooms expects', () => {
    expect(toApiTime('08:30')).toBe('08:30:00');
  });

  it('leaves a time that already has seconds', () => {
    expect(toApiTime('08:30:15')).toBe('08:30:15');
  });
});
