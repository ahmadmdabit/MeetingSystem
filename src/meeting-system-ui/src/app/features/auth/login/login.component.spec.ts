import { ComponentFixture, TestBed } from '@angular/core/testing';
import { LoginComponent } from './login.component';
import { ReactiveFormsModule } from '@angular/forms';
// Import provideRouter to create a functional router for testing
import { Router, ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { AuthService } from '../../../core/api/auth.service';
import { Observable, of, throwError } from 'rxjs';
import { provideZonelessChangeDetection } from '@angular/core';
import { Login, AuthResponse } from '../../../core/models/auth.model';

// Mocks
class MockAuthService {
  login(credentials: Login): Observable<AuthResponse> {
    return of({ token: 'fake-jwt-token' });
  }
}

// The MockRouter class is no longer needed.

describe('LoginComponent', () => {
  let component: LoginComponent;
  let fixture: ComponentFixture<LoginComponent>;
  let mockAuthService: MockAuthService;
  // We will get the router from the TestBed and spy on it.
  let router: Router;

  // Helper function to set up the TestBed with specific query params
  const setup = async (queryParams: { [key: string]: string } = {}) => {
    mockAuthService = new MockAuthService();

    await TestBed.configureTestingModule({
      imports: [LoginComponent, ReactiveFormsModule],
      providers: [
        provideZonelessChangeDetection(),
        // FIX: Use provideRouter to set up a functional router that satisfies RouterLink
        provideRouter([]),
        { provide: AuthService, useValue: mockAuthService },
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { queryParamMap: convertToParamMap(queryParams) } }
        }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(LoginComponent);
    component = fixture.componentInstance;
    // Get the router instance from the test's dependency injector
    router = TestBed.inject(Router);
  };

  describe('Standard Initialization', () => {
    beforeEach(async () => {
      await setup();
      fixture.detectChanges();
    });

    it('should create', () => {
      expect(component).toBeTruthy();
    });

    it('should not show success message by default', () => {
      expect(component.showSuccessMessage).toBe(false);
      const successAlert = fixture.nativeElement.querySelector('.alert-success');
      expect(successAlert).toBeFalsy();
    });
  });

  describe('Initialization after Registration', () => {
    it('should show success message if "registered" query param is true', async () => {
      await setup({ registered: 'true' });
      fixture.detectChanges(); // Triggers ngOnInit

      expect(component.showSuccessMessage).toBe(true);
      const successAlert = fixture.nativeElement.querySelector('.alert-success');
      expect(successAlert).toBeTruthy();
      expect(successAlert.textContent).toContain('Registration successful!');
    });
  });

  describe('Form Interaction and Submission', () => {
    let navigateSpy: jasmine.Spy;

    beforeEach(async () => {
      await setup();
      // Create the spy on the injected router instance before each test in this block
      navigateSpy = spyOn(router, 'navigate');
      fixture.detectChanges();
    });

    it('should have an invalid form initially', () => {
      expect(component.loginForm.valid).toBe(false);
    });

    it('should have a valid form when all fields are filled', () => {
      component.loginForm.patchValue({
        email: 'test@example.com',
        password: 'password123'
      });
      expect(component.loginForm.valid).toBe(true);
    });

    it('should call AuthService.login and navigate on successful login', () => {
      const loginSpy = spyOn(mockAuthService, 'login').and.callThrough();
      component.loginForm.patchValue({
        email: 'test@example.com',
        password: 'password123'
      });

      component.onSubmit();

      expect(loginSpy).toHaveBeenCalledWith({
        email: 'test@example.com',
        password: 'password123'
      });
      // Use the navigateSpy to check for navigation
      expect(navigateSpy).toHaveBeenCalledWith(['/dashboard']);
    });

    it('should set the error message on login failure', () => {
      const errorResponse = { error: { message: 'Invalid credentials' } };
      spyOn(mockAuthService, 'login').and.returnValue(throwError(() => errorResponse));
      component.loginForm.patchValue({
        email: 'test@example.com',
        password: 'wrongpassword'
      });

      component.onSubmit();

      expect(component.error).toBe('Invalid credentials');
      expect(component.isLoading).toBe(false);
    });
  });
});
