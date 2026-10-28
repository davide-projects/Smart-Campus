import { Component, OnInit, computed, inject, signal } from '@angular/core';
import {
  AbstractControl,
  FormArray,
  FormControl,
  FormGroup,
  ReactiveFormsModule,
  ValidationErrors,
  Validators,
} from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatChipsModule } from '@angular/material/chips';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { forkJoin, of } from 'rxjs';
import {
  BuildingOption,
  DayOfWeek,
  ROOM_TYPES,
  RoomDetail,
  RoomEquipment,
  RoomType,
  RoomWriteRequest,
  WEEK_DAYS,
  dayOfWeekLabel,
  roomTypeLabel,
} from '../../models/room.model';
import { problemOf } from '../../services/api-problem';
import { RoomsService } from '../../services/rooms.service';

// Orari proposti quando si apre un giorno: l'utente li cambia se serve.
export const DEFAULT_OPENS_AT = '08:00';
export const DEFAULT_CLOSES_AT = '20:00';

// Messaggi di errore dei campi, gli stessi del backend dove esiste una regola equivalente.
export const MESSAGES = {
  nameRequired: 'Inserisci il nome dello spazio.',
  nameTooLong: 'Il nome può avere al massimo 100 caratteri.',
  buildingRequired: "Scegli l'edificio.",
  typeRequired: 'Scegli il tipo di spazio.',
  floorInvalid: 'Il piano deve essere un numero intero.',
  capacityRequired: 'Inserisci la capienza.',
  capacityInvalid: 'La capienza deve essere un numero intero maggiore di zero.',
  timeRequired: 'Indica orario di apertura e di chiusura.',
  closesBeforeOpens: "L'orario di chiusura deve essere successivo all'apertura.",
  notFound: 'Questo spazio non esiste più o è stato dismesso.',
  forbidden: 'Non hai i permessi per gestire gli spazi.',
  generic: 'Salvataggio non riuscito. Riprova tra poco.',
  invalidData: 'Alcuni dati non sono validi. Controlla i campi e riprova.',
  loadFailed: 'Impossibile caricare i dati. Riprova tra poco.',
} as const;

type DayGroup = FormGroup<{
  open: FormControl<boolean>;
  opensAt: FormControl<string>;
  closesAt: FormControl<string>;
}>;

// Il nome non può essere fatto solo di spazi.
function notBlank(control: AbstractControl): ValidationErrors | null {
  return typeof control.value === 'string' && control.value.trim() === '' ? { required: true } : null;
}

function maxTrimmedLength(max: number) {
  return (control: AbstractControl): ValidationErrors | null =>
    typeof control.value === 'string' && control.value.trim().length > max ? { maxlength: true } : null;
}

function integer(control: AbstractControl): ValidationErrors | null {
  const value = control.value as number | null;
  return value === null || Number.isInteger(value) ? null : { integer: true };
}

// Un giorno aperto ha bisogno di entrambi gli orari e la chiusura deve seguire l'apertura.
export function openingDayValidator(group: AbstractControl): ValidationErrors | null {
  const { open, opensAt, closesAt } = (group as DayGroup).getRawValue();
  if (!open) {
    return null;
  }
  if (!opensAt || !closesAt) {
    return { timeRequired: true };
  }
  // Orari "HH:mm" con zeri iniziali: il confronto tra stringhe segue l'ordine delle ore.
  return closesAt <= opensAt ? { closesBeforeOpens: true } : null;
}

// "08:00" diventa "08:00:00", il formato che Rooms accetta e restituisce.
export function toApiTime(time: string): string {
  return time.length === 5 ? `${time}:00` : time;
}

function toInputTime(time: string | null): string {
  return time ? time.slice(0, 5) : '';
}

@Component({
  imports: [
    ReactiveFormsModule,
    RouterLink,
    MatButtonModule,
    MatCardModule,
    MatCheckboxModule,
    MatChipsModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    MatSelectModule,
    MatSlideToggleModule,
  ],
  selector: 'app-room-form',
  styleUrl: './room-form.scss',
  templateUrl: './room-form.html',
})
export class RoomForm implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly rooms = inject(RoomsService);

  // Con un id nella rotta si modifica uno spazio, altrimenti se ne crea uno nuovo.
  protected readonly roomId = this.route.snapshot.paramMap.get('id');
  protected readonly isEdit = this.roomId !== null;

  protected readonly types = ROOM_TYPES;
  protected readonly typeLabel = roomTypeLabel;
  protected readonly dayLabel = dayOfWeekLabel;

  protected readonly form = new FormGroup({
    name: new FormControl('', {
      nonNullable: true,
      validators: [notBlank, maxTrimmedLength(100)],
    }),
    buildingId: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    type: new FormControl<RoomType>('', { nonNullable: true, validators: [Validators.required] }),
    floor: new FormControl<number | null>(0, [Validators.required, integer]),
    capacity: new FormControl<number | null>(null, [Validators.required, Validators.min(1), integer]),
    isBookable: new FormControl(true, { nonNullable: true }),
    days: new FormArray<DayGroup>(WEEK_DAYS.map(() => this.newDay())),
  });

  protected readonly days = WEEK_DAYS;

  protected readonly buildings = signal<BuildingOption[]>([]);
  // Le attrezzature non si modificano da qui finché Rooms non ne espone l'elenco: in modifica
  // vengono rimandate così come sono, perché la PUT sostituisce la lista e altrimenti le cancellerebbe.
  protected readonly equipment = signal<RoomEquipment[]>([]);

  protected readonly loading = signal(true);
  protected readonly saving = signal(false);
  protected readonly notFound = signal(false);
  protected readonly loadError = signal<string | null>(null);
  protected readonly formError = signal<string | null>(null);
  // Errore sugli orari che il backend non lega a un giorno preciso.
  protected readonly hoursError = signal<string | null>(null);
  protected readonly submitted = signal(false);

  protected readonly heading = computed(() => (this.isEdit ? 'Modifica spazio' : 'Nuovo spazio'));

  ngOnInit(): void {
    forkJoin({
      buildings: this.rooms.listBuildings(),
      room: this.roomId ? this.rooms.getRoomById(this.roomId) : of(null),
    }).subscribe({
      next: ({ buildings, room }) => {
        this.buildings.set(buildings);
        if (room) {
          this.fill(room);
        }
        this.loading.set(false);
      },
      error: (error: unknown) => {
        this.loading.set(false);
        if (problemOf(error).status === 404) {
          this.notFound.set(true);
        } else {
          this.loadError.set(MESSAGES.loadFailed);
        }
      },
    });
  }

  protected get dayGroups(): DayGroup[] {
    return this.form.controls.days.controls;
  }

  protected isOpen(index: number): boolean {
    return this.dayGroups[index].controls.open.value;
  }

  // Messaggio da mostrare sotto un campo, o null se è a posto o ancora intatto.
  protected fieldError(name: 'name' | 'buildingId' | 'type' | 'floor' | 'capacity'): string | null {
    const control = this.form.controls[name];
    const errors = control.errors;
    if (!errors || !(control.touched || this.submitted())) {
      return null;
    }
    if (typeof errors['server'] === 'string') {
      return errors['server'];
    }

    switch (name) {
      case 'name':
        return errors['maxlength'] ? MESSAGES.nameTooLong : MESSAGES.nameRequired;
      case 'buildingId':
        return MESSAGES.buildingRequired;
      case 'type':
        return MESSAGES.typeRequired;
      case 'floor':
        return MESSAGES.floorInvalid;
      case 'capacity':
        return errors['required'] ? MESSAGES.capacityRequired : MESSAGES.capacityInvalid;
    }
  }

  protected dayError(index: number): string | null {
    const group = this.dayGroups[index];
    if (!(group.touched || this.submitted())) {
      return null;
    }
    if (group.hasError('timeRequired')) {
      return MESSAGES.timeRequired;
    }
    return group.hasError('closesBeforeOpens') ? MESSAGES.closesBeforeOpens : null;
  }

  protected submit(): void {
    this.submitted.set(true);
    this.formError.set(null);
    this.hoursError.set(null);

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    const request = this.toRequest();

    const done = (id: string) => void this.router.navigate(['/rooms', id]);
    const failed = (error: unknown) => {
      this.saving.set(false);
      this.showServerError(error);
    };

    if (this.roomId) {
      const id = this.roomId;
      this.rooms.update(id, request).subscribe({ next: () => done(id), error: failed });
    } else {
      this.rooms.create(request).subscribe({ next: (created) => done(created.id), error: failed });
    }
  }

  private newDay(): DayGroup {
    return new FormGroup(
      {
        open: new FormControl(false, { nonNullable: true }),
        opensAt: new FormControl(DEFAULT_OPENS_AT, { nonNullable: true }),
        closesAt: new FormControl(DEFAULT_CLOSES_AT, { nonNullable: true }),
      },
      { validators: openingDayValidator },
    );
  }

  // Precompila il modulo con lo spazio da modificare.
  private fill(room: RoomDetail): void {
    this.form.patchValue({
      name: room.name,
      buildingId: room.buildingId,
      type: room.type,
      floor: room.floor,
      capacity: room.capacity,
      isBookable: room.isBookable,
    });

    WEEK_DAYS.forEach((day, index) => {
      const hours = room.openingHours.find((item) => item.dayOfWeek === day);
      if (hours && !hours.isClosed) {
        this.dayGroups[index].setValue({
          open: true,
          opensAt: toInputTime(hours.opensAt),
          closesAt: toInputTime(hours.closesAt),
        });
      }
    });

    this.equipment.set(room.equipment);
  }

  private toRequest(): RoomWriteRequest {
    const value = this.form.getRawValue();

    return {
      buildingId: value.buildingId,
      name: value.name.trim(),
      type: value.type,
      capacity: value.capacity as number,
      floor: value.floor as number,
      isBookable: value.isBookable,
      equipmentIds: this.equipment().map((item) => item.id),
      openingHours: value.days.flatMap((day, index) =>
        day.open
          ? [
              {
                dayOfWeek: WEEK_DAYS[index] as DayOfWeek,
                opensAt: toApiTime(day.opensAt),
                closesAt: toApiTime(day.closesAt),
              },
            ]
          : [],
      ),
    };
  }

  // Il messaggio resta sul campo finché l'utente non lo modifica.
  private rejectField(
    field: 'name' | 'buildingId' | 'type' | 'floor' | 'capacity',
    message: string,
  ): void {
    const control = this.form.controls[field];
    control.setErrors({ server: message });
    control.markAsTouched();
  }

  // Mostra il rifiuto del backend: sul campo quando lo indica, altrimenti in cima al modulo.
  private showServerError(error: unknown): void {
    const problem = problemOf(error);
    const fields = ['name', 'buildingId', 'type', 'floor', 'capacity'] as const;

    if (problem.status === 409) {
      this.rejectField(
        'name',
        problem.detail ?? "Esiste già uno spazio con questo nome nell'edificio indicato.",
      );
      return;
    }
    if (problem.status === 404) {
      this.formError.set(MESSAGES.notFound);
      return;
    }
    if (problem.status === 403) {
      this.formError.set(MESSAGES.forbidden);
      return;
    }
    if (problem.status !== 400) {
      this.formError.set(MESSAGES.generic);
      return;
    }

    let placed = false;
    for (const field of fields) {
      const message = problem.errors[field]?.[0];
      if (message) {
        this.rejectField(field, message);
        placed = true;
      }
    }
    const hours = problem.errors['openingHours']?.[0];
    if (hours) {
      this.hoursError.set(hours);
      placed = true;
    }
    const equipment = problem.errors['equipmentIds']?.[0];
    if (equipment) {
      this.formError.set(equipment);
      placed = true;
    }
    if (!placed) {
      this.formError.set(MESSAGES.invalidData);
    }
  }
}
