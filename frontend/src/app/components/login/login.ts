import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { ActivatedRoute, Router } from '@angular/router';
import { AuthService } from '../../services/auth.service';

const DEFAULT_PAGE = '/rooms';

@Component({
  imports: [MatButtonModule, MatFormFieldModule, MatInputModule, ReactiveFormsModule],
  selector: 'app-login',
  styleUrl: './login.scss',
  templateUrl: './login.html',
})
export class Login {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  private readonly query = inject(ActivatedRoute).snapshot.queryParamMap;

  protected readonly form = new FormGroup({
    username: new FormControl('', { nonNullable: true, validators: Validators.required }),
    password: new FormControl('', { nonNullable: true, validators: Validators.required }),
  });

  protected readonly sessionExpired = this.query.get('reason') === 'expired';
  protected readonly submitting = signal(false);
  protected readonly errorMessage = signal<string | null>(null);

  protected submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    this.errorMessage.set(null);
    const { username, password } = this.form.getRawValue();

    this.auth
      .login(username.trim(), password)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => void this.router.navigateByUrl(safeReturnUrl(this.query.get('returnUrl'))),
        error: (error: unknown) => {
          this.submitting.set(false);
          this.errorMessage.set(
            error instanceof HttpErrorResponse && error.status === 401
              ? 'Nome utente o password non corretti.'
              : 'Non è stato possibile accedere. Riprova più tardi.',
          );
        },
      });
  }
}

// Si torna solo a pagine interne dell'app: un returnUrl esterno (es. "//sito.it") viene ignorato.
function safeReturnUrl(url: string | null): string {
  if (!url || !url.startsWith('/') || url.startsWith('//') || url.startsWith('/login')) {
    return DEFAULT_PAGE;
  }
  return url;
}
