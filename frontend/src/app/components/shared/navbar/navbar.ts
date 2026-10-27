import { Component } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatToolbarModule } from '@angular/material/toolbar';

interface NavLink {
  path: string;
  label: string;
}

@Component({
  imports: [MatButtonModule, MatIconModule, MatMenuModule, MatToolbarModule, RouterLink, RouterLinkActive],
  selector: 'app-navbar',
  styleUrl: './navbar.scss',
  templateUrl: './navbar.html',
})
export class Navbar {
  // Voci di navigazione: per una nuova pagina basta aggiungerla qui.
  protected readonly links: NavLink[] = [{ path: '/rooms', label: 'Cerca spazi' }];
}
