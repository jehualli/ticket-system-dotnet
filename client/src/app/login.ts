import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { errorMessage } from './api';
import { Auth } from './auth';

@Component({
  selector: 'app-login',
  imports: [FormsModule],
  template: `
    <form class="card login" #form="ngForm" (ngSubmit)="submit()">
      <h1>Sign in</h1>
      <label>Email <input name="email" type="email" [(ngModel)]="email" required autocomplete="username" /></label>
      <label>Password <input name="password" type="password" [(ngModel)]="password" required autocomplete="current-password" /></label>
      @if (error()) {
        <p class="error">{{ error() }}</p>
      }
      <button type="submit" [disabled]="busy() || form.invalid">{{ busy() ? 'Signing in…' : 'Sign in' }}</button>
      <p class="hint">Demo: admin&#64;company.com / Admin&#64;123 · client1&#64;acme.com / Client&#64;123 · dev1&#64;company.com / Dev&#64;123</p>
    </form>
  `,
})
export class Login {
  private readonly auth = inject(Auth);
  private readonly router = inject(Router);

  email = '';
  password = '';
  readonly busy = signal(false);
  readonly error = signal('');

  submit(): void {
    this.busy.set(true);
    this.error.set('');
    this.auth.login(this.email, this.password).subscribe({
      next: () => this.router.navigateByUrl('/tickets'),
      error: (err) => {
        this.error.set(errorMessage(err));
        this.busy.set(false);
      },
    });
  }
}
