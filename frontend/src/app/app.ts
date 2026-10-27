import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { Footer } from './components/shared/footer/footer';
import { Navbar } from './components/shared/navbar/navbar';

@Component({
  imports: [Footer, Navbar, RouterOutlet],
  selector: 'app-root',
  styleUrl: './app.scss',
  templateUrl: './app.html',
})
export class App {}
