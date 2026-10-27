import { HttpErrorResponse } from '@angular/common/http';
import { HarnessLoader } from '@angular/cdk/testing';
import { TestbedHarnessEnvironment } from '@angular/cdk/testing/testbed';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MatButtonHarness } from '@angular/material/button/testing';
import { MatInputHarness } from '@angular/material/input/testing';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { Observable, of, throwError } from 'rxjs';
import { UserProfile } from '../../models/user.model';
import { AuthService } from '../../services/auth.service';
import { Login } from './login';

// Dati sintetici di prova.
const USER: UserProfile = { id: 'u1', username: 'student1', displayName: 'Mario Rossi', role: 'Student' };

describe('Login', () => {
  let fixture: ComponentFixture<Login>;
  let element: HTMLElement;
  let loader: HarnessLoader;
  let login: ReturnType<typeof vi.fn<(username: string, password: string) => Observable<UserProfile>>>;
  let router: Router;

  async function render(queryParams: Record<string, string> = {}): Promise<void> {
    await TestBed.configureTestingModule({
      imports: [Login],
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: { login } },
        { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: convertToParamMap(queryParams) } } },
      ],
    }).compileComponents();

    router = TestBed.inject(Router);
    vi.spyOn(router, 'navigateByUrl').mockResolvedValue(true);
    fixture = TestBed.createComponent(Login);
    element = fixture.nativeElement;
    loader = TestbedHarnessEnvironment.loader(fixture);
    await fixture.whenStable();
  }

  async function fillAndSubmit(username: string, password: string): Promise<void> {
    await (await loader.getHarness(MatInputHarness.with({ selector: '[formControlName="username"]' }))).setValue(username);
    await (await loader.getHarness(MatInputHarness.with({ selector: '[formControlName="password"]' }))).setValue(password);
    await (await loader.getHarness(MatButtonHarness.with({ text: 'Accedi' }))).click();
    await fixture.whenStable();
  }

  beforeEach(() => {
    login = vi.fn(() => of(USER));
  });

  it('logs in and opens the room search by default', async () => {
    await render();

    await fillAndSubmit(' student1 ', 'segreta');

    expect(login).toHaveBeenCalledWith('student1', 'segreta');
    expect(router.navigateByUrl).toHaveBeenCalledWith('/rooms');
  });

  it('goes back to the page that required the login', async () => {
    await render({ returnUrl: '/my-bookings' });

    await fillAndSubmit('student1', 'segreta');

    expect(router.navigateByUrl).toHaveBeenCalledWith('/my-bookings');
  });

  it('ignores a return address outside the app', async () => {
    await render({ returnUrl: '//example.com' });

    await fillAndSubmit('student1', 'segreta');

    expect(router.navigateByUrl).toHaveBeenCalledWith('/rooms');
  });

  it('does not call the backend when a field is empty', async () => {
    await render();

    await fillAndSubmit('student1', '');

    expect(login).not.toHaveBeenCalled();
    expect(element.textContent).toContain('Inserisci la password.');
  });

  it('shows a message when the credentials are wrong', async () => {
    login.mockReturnValue(throwError(() => new HttpErrorResponse({ status: 401 })));
    await render();

    await fillAndSubmit('student1', 'sbagliata');

    expect(element.querySelector('[role="alert"]')!.textContent).toContain('Nome utente o password non corretti.');
    expect(router.navigateByUrl).not.toHaveBeenCalled();
  });

  it('shows a different message when the service does not respond', async () => {
    login.mockReturnValue(throwError(() => new HttpErrorResponse({ status: 0 })));
    await render();

    await fillAndSubmit('student1', 'segreta');

    expect(element.querySelector('[role="alert"]')!.textContent).toContain(
      'Non è stato possibile accedere. Riprova più tardi.',
    );
  });

  it('tells the user when the session has expired', async () => {
    await render({ reason: 'expired' });

    expect(element.textContent).toContain('La sessione è scaduta, accedi di nuovo.');
  });
});
