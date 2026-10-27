import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Title } from '@angular/platform-browser';
import { Router, TitleStrategy, provideRouter } from '@angular/router';
import { AppTitleStrategy } from './app-title.strategy';

@Component({ template: '' })
class EmptyPage {}

describe('AppTitleStrategy', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([
          { path: 'with-title', title: 'Cerca uno spazio', component: EmptyPage },
          { path: 'without-title', component: EmptyPage },
        ]),
        { provide: TitleStrategy, useClass: AppTitleStrategy },
      ],
    });
  });

  it('appends the app name to the route title', async () => {
    await TestBed.inject(Router).navigateByUrl('/with-title');

    expect(TestBed.inject(Title).getTitle()).toBe('Cerca uno spazio · SmartCampus');
  });

  it('uses only the app name when the route has no title', async () => {
    await TestBed.inject(Router).navigateByUrl('/without-title');

    expect(TestBed.inject(Title).getTitle()).toBe('SmartCampus');
  });
});
