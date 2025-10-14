import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { provideZonelessChangeDetection } from '@angular/core';

import { authInterceptor } from './auth.interceptor';
import { AuthService } from '../api/auth.service';

class AuthServiceStub {
  token: string | null = 'token-123';
  getToken(): string | null {
    return this.token;
  }
}

describe('authInterceptor', () => {
  let httpClient: HttpClient;
  let httpController: HttpTestingController;
  let authServiceStub: AuthServiceStub;

  beforeEach(() => {
    authServiceStub = new AuthServiceStub();

    TestBed.configureTestingModule({
      providers: [
        provideZonelessChangeDetection(),
        {
          provide: AuthService,
          useValue: authServiceStub
        },
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
      ]
    });

    httpClient = TestBed.inject(HttpClient);
    httpController = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpController.verify());

  it('should attach Authorization header when token exists', () => {
    httpClient.get('/data').subscribe();

    const req = httpController.expectOne('/data');
    expect(req.request.headers.get('Authorization')).toBe('Bearer token-123');
    req.flush({});
  });

  it('should not attach Authorization header when token is absent', () => {
    authServiceStub.token = null;
    httpClient.get('/data').subscribe();

    const req = httpController.expectOne('/data');
    expect(req.request.headers.has('Authorization')).toBeFalse();
    req.flush({});
  });
});
