import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { provideZonelessChangeDetection } from '@angular/core'; // <--- IMPORT THIS
import { AuthService } from './auth.service';
import { API_CONFIG, DEFAULT_API_CONFIG } from '../config/api.config';
import { AuthResponse, Login, RegisterUser } from '../models/auth.model';

describe('AuthService', () => {
  let service: AuthService;
  let httpMock: HttpTestingController;
  const API_URL = `${DEFAULT_API_CONFIG.baseUrl}/Auth`;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [
        // This is the critical fix: Align the test environment with the app config
        provideZonelessChangeDetection(),
        AuthService,
        { provide: API_CONFIG, useValue: DEFAULT_API_CONFIG }
      ]
    });
    service = TestBed.inject(AuthService);
    httpMock = TestBed.inject(HttpTestingController);

    // Clear local storage before each test to ensure isolation
    localStorage.clear();
  });

  afterEach(() => {
    httpMock.verify(); // This will now work correctly
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  describe('register', () => {
    it('should send a POST request with FormData to the register endpoint', () => {
      const mockUser: RegisterUser = {
        firstName: 'John',
        email: 'john@example.com',
        password: 'password123'
      };

      service.register(mockUser).subscribe();

      const req = httpMock.expectOne(`${API_URL}/register`);
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toBeInstanceOf(FormData);
      expect(req.request.body.get('firstName')).toBe('John');
      expect(req.request.body.get('email')).toBe('john@example.com');

      req.flush(null);
    });
    
    // Test for register with 400 Bad Request
    it('should handle registration failure with bad request', () => {
      const mockUser: RegisterUser = {
        firstName: 'John',
        email: 'invalid-email', // This might cause a 400
        password: 'short' // This might cause a 400
      };

      service.register(mockUser).subscribe({
        error: (error) => {
          expect(error.status).toBe(400);
        }
      });

      const req = httpMock.expectOne(`${API_URL}/register`);
      req.flush({ message: 'Validation failed' }, { status: 400, statusText: 'Bad Request' });
    });
  });

  describe('login', () => {
    it('should send a POST request and save the token on success', () => {
      const credentials: Login = { email: 'test@test.com', password: 'password' };
      const mockResponse: AuthResponse = { token: 'fake-jwt-token' };
      const setItemSpy = spyOn(localStorage, 'setItem').and.callThrough();

      service.login(credentials).subscribe(response => {
        expect(response).toEqual(mockResponse);
      });

      const req = httpMock.expectOne(`${API_URL}/login`);
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual(credentials);
      req.flush(mockResponse);

      expect(setItemSpy).toHaveBeenCalledWith('auth_token', 'fake-jwt-token');
      expect(localStorage.getItem('auth_token')).toBe('fake-jwt-token');
    });

    it('should not save a token on login failure', () => {
      const credentials: Login = { email: 'fail@test.com', password: 'wrong' };
      const setItemSpy = spyOn(localStorage, 'setItem');

      service.login(credentials).subscribe({
        error: err => {
          expect(err).toBeTruthy();
        }
      });

      const req = httpMock.expectOne(`${API_URL}/login`);
      req.flush({ message: 'Invalid credentials' }, { status: 401, statusText: 'Unauthorized' });

      expect(setItemSpy).not.toHaveBeenCalled();
    });
  });

  describe('logout', () => {
    it('should send a POST request and clear the token on success', () => {
      localStorage.setItem('auth_token', 'some-token-to-remove');
      const removeItemSpy = spyOn(localStorage, 'removeItem').and.callThrough();

      service.logout().subscribe();

      const req = httpMock.expectOne(`${API_URL}/logout`);
      expect(req.request.method).toBe('POST');
      req.flush(null);

      expect(removeItemSpy).toHaveBeenCalledWith('auth_token');
      expect(localStorage.getItem('auth_token')).toBeNull();
    });

    // --- ENHANCEMENT: Test that the token is cleared even if the API call fails ---
    it('should clear the token even if the logout API request fails', () => {
        localStorage.setItem('auth_token', 'some-token-to-remove');
        const removeItemSpy = spyOn(localStorage, 'removeItem').and.callThrough();

        service.logout().subscribe({
            error: (err) => {
                expect(err).toBeTruthy();
            }
        });

        const req = httpMock.expectOne(`${API_URL}/logout`);
        req.error(new ProgressEvent('error'));

        // The key behavior: The local token should be gone regardless of API success
        expect(removeItemSpy).toHaveBeenCalledWith('auth_token');
        expect(localStorage.getItem('auth_token')).toBeNull();
    });
    
    // Test for logout with 400 Bad Request
    it('should handle logout failure with bad request', () => {
      localStorage.setItem('auth_token', 'some-token');
      const removeItemSpy = spyOn(localStorage, 'removeItem').and.callThrough();

      service.logout().subscribe({
        error: (error) => {
          expect(error.status).toBe(400);
        }
      });

      const req = httpMock.expectOne(`${API_URL}/logout`);
      req.flush({ message: 'Bad Request' }, { status: 400, statusText: 'Bad Request' });

      // Token should still be cleared even on server error
      expect(removeItemSpy).toHaveBeenCalledWith('auth_token');
      expect(localStorage.getItem('auth_token')).toBeNull();
    });
  });

  describe('getToken', () => {
    it('should return the token from localStorage if it exists', () => {
      localStorage.setItem('auth_token', 'my-test-token');
      expect(service.getToken()).toBe('my-test-token');
    });

    it('should return null if the token does not exist in localStorage', () => {
      expect(service.getToken()).toBeNull();
    });
  });
});
