import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { Observable, catchError, tap, throwError } from 'rxjs';
import { Api, AuthResponse } from './api';

const STORAGE_KEY = 'ticket-system.session';

@Injectable({ providedIn: 'root' })
export class Auth {
  private readonly api = inject(Api);
  private readonly session = signal<AuthResponse | null>(readSession());

  readonly user = this.session.asReadonly();
  readonly token = computed(() => this.session()?.token ?? null);
  readonly isStaff = computed(() => ['ADMIN', 'AGENT'].includes(this.session()?.role ?? ''));
  readonly isClient = computed(() => this.session()?.role === 'CLIENT');

  login(email: string, password: string): Observable<AuthResponse> {
    return this.api.login(email, password).pipe(tap((session) => this.setSession(session)));
  }

  logout(): void {
    this.setSession(null);
  }

  private setSession(session: AuthResponse | null): void {
    this.session.set(session);
    try {
      if (session) localStorage.setItem(STORAGE_KEY, JSON.stringify(session));
      else localStorage.removeItem(STORAGE_KEY);
    } catch {
      // Storage can be unavailable (private mode); the session just won't survive a reload.
    }
  }
}

function readSession(): AuthResponse | null {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    return raw ? (JSON.parse(raw) as AuthResponse) : null;
  } catch {
    return null;
  }
}

// Adds "Authorization: Bearer <token>" to API calls, and sends the user to /login when the token expires.
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(Auth);
  const router = inject(Router);
  const token = auth.token();

  const request = token && req.url.startsWith('/api/')
    ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } })
    : req;

  return next(request).pipe(
    catchError((err: HttpErrorResponse) => {
      if (err.status === 401 && token) {
        auth.logout();
        router.navigateByUrl('/login');
      }
      return throwError(() => err);
    }),
  );
};

export const authGuard: CanActivateFn = () =>
  inject(Auth).user() ? true : inject(Router).parseUrl('/login');
