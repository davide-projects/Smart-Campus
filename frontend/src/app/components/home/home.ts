import { Component } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';

// Pagina iniziale dell'app: presenta SmartCampus e porta alla ricerca degli spazi.
// «Accedi» e «Le mie prenotazioni» restano nella navbar.
@Component({
  imports: [MatButtonModule, MatIconModule, RouterLink],
  selector: 'app-home',
  styleUrl: './home.scss',
  templateUrl: './home.html',
})
export class Home {}
