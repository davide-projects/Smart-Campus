import { Component, computed, input } from '@angular/core';
import { MatTooltipModule } from '@angular/material/tooltip';
import { AvailabilitySlot, buildTimeline } from '../../../models/availability.model';

// Disponibilità di una giornata come linea del tempo: una barra con un blocco per ogni tratto libero
// o occupato, al posto di una card per ogni fascia da 30 minuti. Mostra solo gli stati, mai chi ha prenotato.
@Component({
  imports: [MatTooltipModule],
  selector: 'app-availability-timeline',
  styleUrl: './availability-timeline.scss',
  templateUrl: './availability-timeline.html',
})
export class AvailabilityTimeline {
  // Fasce da 30 minuti, come le restituisce l'API (contigue, dall'apertura alla chiusura).
  readonly slots = input.required<AvailabilitySlot[]>();

  protected readonly timeline = computed(() => buildTimeline(this.slots()));
}
