import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { Auth, authInterceptor } from './auth';

describe('authInterceptor', () => {
  let http: HttpClient;
  let backend: HttpTestingController;
  let auth: Auth;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpClient);
    backend = TestBed.inject(HttpTestingController);
    auth = TestBed.inject(Auth);
  });

  afterEach(() => backend.verify());

  function signIn(): void {
    auth.login('a@b.c', 'pw').subscribe();
    backend.expectOne('/api/auth/login').flush({
      token: 'abc123', userId: '1', name: 'Alice', email: 'a@b.c', role: 'CLIENT',
    });
  }

  it('sends no Authorization header before login', () => {
    http.get('/api/tickets').subscribe();

    expect(backend.expectOne('/api/tickets').request.headers.has('Authorization')).toBe(false);
  });

  it('adds the bearer token to API calls after login', () => {
    signIn();

    http.get('/api/tickets').subscribe();

    expect(backend.expectOne('/api/tickets').request.headers.get('Authorization')).toBe('Bearer abc123');
    expect(auth.isClient()).toBe(true);
  });

  it('signs out and goes to /login when the API answers 401', () => {
    signIn();
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);

    http.get('/api/tickets').subscribe({ error: () => {} });
    backend.expectOne('/api/tickets').flush(null, { status: 401, statusText: 'Unauthorized' });

    expect(auth.user()).toBeNull();
    expect(navigate).toHaveBeenCalledWith('/login');
  });
});
