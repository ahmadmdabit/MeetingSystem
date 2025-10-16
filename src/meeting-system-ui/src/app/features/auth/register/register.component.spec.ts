import { ComponentFixture, TestBed } from '@angular/core/testing';
import { RegisterComponent } from './register.component';
import { ReactiveFormsModule } from '@angular/forms';
import { Router, provideRouter } from '@angular/router';
import { AuthService } from '../../../core/api/auth.service';
import { of, throwError } from 'rxjs';
import { provideZonelessChangeDetection } from '@angular/core';
import { RegisterUser } from '../../../core/models/auth.model';

// Mocks
class MockAuthService {
  register(user: RegisterUser) {
    return of(undefined); // Return Observable<void>
  }
}

describe('RegisterComponent', () => {
  let component: RegisterComponent;
  let fixture: ComponentFixture<RegisterComponent>;
  let mockAuthService: MockAuthService;
  let router: Router;

  beforeEach(async () => {
    mockAuthService = new MockAuthService();

    await TestBed.configureTestingModule({
      imports: [RegisterComponent, ReactiveFormsModule],
      providers: [
        provideZonelessChangeDetection(),
        provideRouter([]),
        { provide: AuthService, useValue: mockAuthService },
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(RegisterComponent);
    component = fixture.componentInstance;
    router = TestBed.inject(Router);
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  describe('Form Validation', () => {
    it('should have an invalid form initially', () => {
      expect(component.registerForm.valid).toBe(false);
    });

    it('should have a valid form when all required fields are filled correctly', () => {
      component.registerForm.patchValue({
        firstName: 'John',
        lastName: 'Doe',
        email: 'john.doe@example.com',
        password: 'password123',
        confirmPassword: 'password123'
      });
      expect(component.registerForm.valid).toBe(true);
    });

    it('should have an invalid form if passwords do not match', () => {
      component.registerForm.patchValue({
        firstName: 'John',
        lastName: 'Doe',
        email: 'john.doe@example.com',
        password: 'password123',
        confirmPassword: 'differentpassword'
      });
      component.registerForm.get('confirmPassword')?.markAsDirty();
      component.registerForm.updateValueAndValidity();

      expect(component.registerForm.valid).toBe(false);
      expect(component.registerForm.hasError('mustMatch')).toBe(true);
    });
  });

  describe('Form Submission', () => {
    let navigateSpy: jasmine.Spy;

    beforeEach(() => {
      navigateSpy = spyOn(router, 'navigate');
      component.registerForm.patchValue({
        firstName: 'John',
        lastName: 'Doe',
        email: 'john.doe@example.com',
        phone: '12345',
        password: 'password123',
        confirmPassword: 'password123',
        profilePicture: null
      });
    });

    it('should call AuthService.register and navigate on successful registration', () => {
      const registerSpy = spyOn(mockAuthService, 'register').and.returnValue(of(undefined));

      component.onSubmit();

      expect(registerSpy).toHaveBeenCalledWith({
        firstName: 'John',
        lastName: 'Doe',
        email: 'john.doe@example.com',
        phone: '12345',
        password: 'password123',
        profilePicture: undefined
      });

      expect(navigateSpy).toHaveBeenCalledWith(['/login'], {
        queryParams: { registered: 'true' }
      });
    });

    it('should include the profile picture in the payload if selected', () => {
      const registerSpy = spyOn(mockAuthService, 'register').and.returnValue(of(undefined));
      const mockFile = new File([''], 'profile.jpg', { type: 'image/jpeg' });
      component.registerForm.patchValue({ profilePicture: mockFile });

      component.onSubmit();

      expect(registerSpy).toHaveBeenCalledWith(jasmine.objectContaining({
        profilePicture: mockFile
      }));
    });

    it('should set the error message on registration failure', () => {
      const errorResponse = { error: { message: 'Email already exists' } };
      spyOn(mockAuthService, 'register').and.returnValue(throwError(() => errorResponse));

      component.onSubmit();

      expect(component.error).toBe('Email already exists');
      expect(component.isLoading).toBe(false);
    });
  });

  describe('File Handling', () => {
    it('should patch the profilePicture form control on file selection', () => {
      const mockFile = new File([''], 'profile.jpg', { type: 'image/jpeg' });

      // FIX: Use the DataTransfer API to create a valid FileList object
      const dataTransfer = new DataTransfer();
      dataTransfer.items.add(mockFile);

      const inputElement: HTMLInputElement = fixture.nativeElement.querySelector('#profilePicture');
      // The 'files' property is read-only, but we can assign our valid DataTransfer object to it in a test
      Object.defineProperty(inputElement, 'files', {
        value: dataTransfer.files,
        configurable: true // Allow the property to be redefined
      });

      // Dispatch a 'change' event to trigger the component's (change) handler
      inputElement.dispatchEvent(new Event('change'));
      fixture.detectChanges();

      expect(component.registerForm.get('profilePicture')?.value).toBe(mockFile);
    });
  });
});
