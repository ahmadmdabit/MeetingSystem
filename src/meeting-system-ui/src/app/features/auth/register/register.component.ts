import { Component, inject, ChangeDetectorRef } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import {
  AbstractControl,
  FormBuilder,
  ReactiveFormsModule,
  ValidationErrors,
  ValidatorFn,
  Validators
} from '@angular/forms';
import { AuthService } from '../../../core/api/auth.service';
import { RegisterUser } from '../../../core/models/auth.model';

/**
 * Custom validator to check if two fields in a form group match.
 * @returns A ValidatorFn that performs the check.
 */
export const passwordMatchValidator: ValidatorFn = (
  control: AbstractControl
): ValidationErrors | null => {
  const password = control.get('password');
  const confirmPassword = control.get('confirmPassword');

  // Return if controls are not found or if confirmPassword hasn't been touched yet
  if (!password || !confirmPassword || !confirmPassword.dirty) {
    return null;
  }

  return password.value === confirmPassword.value ? null : { mustMatch: true };
};

@Component({
  selector: 'app-register',
  standalone: true,
  // FIX: Use @if, so NgIf is no longer needed.
  imports: [ReactiveFormsModule, RouterLink],
  template: `
    <div class="register-container">
      <a routerLink="/dashboard" class="logo-link">
        <img src="assets/images/meeting-system.png" alt="Meeting System Logo" class="logo-icon" />
      </a>
      <div class="register-form">
        <h2>Register</h2>
        <form [formGroup]="registerForm" (ngSubmit)="onSubmit()">
          <div class="form-row">
            <div class="form-group">
              <label for="firstName">First Name</label>
              <input
                type="text"
                id="firstName"
                formControlName="firstName"
                class="form-control"
                [class.is-invalid]="
                  registerForm.get('firstName')?.invalid && registerForm.get('firstName')?.touched
                "
              />
              <!-- FIX: Use modern @if syntax -->
              @if (
                registerForm.get('firstName')?.invalid && registerForm.get('firstName')?.touched
              ) {
                <div class="invalid-feedback">
                  @if (registerForm.get('firstName')?.errors?.['required']) {
                    <div>First name is required</div>
                  }
                </div>
              }
            </div>

            <div class="form-group">
              <label for="lastName">Last Name</label>
              <input
                type="text"
                id="lastName"
                formControlName="lastName"
                class="form-control"
                [class.is-invalid]="
                  registerForm.get('lastName')?.invalid && registerForm.get('lastName')?.touched
                "
              />
              @if (registerForm.get('lastName')?.invalid && registerForm.get('lastName')?.touched) {
                <div class="invalid-feedback">
                  @if (registerForm.get('lastName')?.errors?.['required']) {
                    <div>Last name is required</div>
                  }
                </div>
              }
            </div>
          </div>

          <div class="form-group">
            <label for="email">Email</label>
            <input
              type="email"
              id="email"
              formControlName="email"
              class="form-control"
              [class.is-invalid]="registerForm.get('email')?.invalid && registerForm.get('email')?.touched"
            />
            @if (registerForm.get('email')?.invalid && registerForm.get('email')?.touched) {
              <div class="invalid-feedback">
                @if (registerForm.get('email')?.errors?.['required']) {
                  <div>Email is required</div>
                }
                @if (registerForm.get('email')?.errors?.['email']) {
                  <div>Please enter a valid email</div>
                }
              </div>
            }
          </div>

          <div class="form-group">
            <label for="phone">Phone</label>
            <input type="tel" id="phone" formControlName="phone" class="form-control" />
          </div>

          <div class="form-group">
            <label for="password">Password</label>
            <input
              type="password"
              id="password"
              formControlName="password"
              class="form-control"
              [class.is-invalid]="
                registerForm.get('password')?.invalid && registerForm.get('password')?.touched
              "
            />
            @if (registerForm.get('password')?.invalid && registerForm.get('password')?.touched) {
              <div class="invalid-feedback">
                @if (registerForm.get('password')?.errors?.['required']) {
                  <div>Password is required</div>
                }
                @if (registerForm.get('password')?.errors?.['minlength']) {
                  <div>Password must be at least 6 characters</div>
                }
              </div>
            }
          </div>

          <div class="form-group">
            <label for="confirmPassword">Confirm Password</label>
            <input
              type="password"
              id="confirmPassword"
              formControlName="confirmPassword"
              class="form-control"
              [class.is-invalid]="
                registerForm.get('confirmPassword')?.invalid &&
                registerForm.get('confirmPassword')?.touched
              "
            />
            @if (
              registerForm.get('confirmPassword')?.invalid &&
              registerForm.get('confirmPassword')?.touched
            ) {
              <div class="invalid-feedback">
                @if (registerForm.get('confirmPassword')?.errors?.['required']) {
                  <div>Please confirm your password</div>
                }
                @if (registerForm.hasError('mustMatch', 'confirmPassword')) {
                  <div>Passwords must match</div>
                }
              </div>
            }
          </div>

          <div class="form-group">
            <label for="profilePicture">Profile Picture</label>
            <input
              type="file"
              id="profilePicture"
              (change)="onFileSelected($event)"
              class="form-control-file"
              accept="image/*"
            />
          </div>

          <button type="submit" class="btn btn-primary" [disabled]="registerForm.invalid || isLoading">
            @if (isLoading) {
              <span class="spinner-border spinner-border-sm mr-1"></span>
            }
            Register
          </button>

          <div class="mt-3">
            <p>Already have an account? <a routerLink="/login">Login here</a></p>
          </div>

          @if (error) {
            <div class="alert alert-danger mt-3">{{ error }}</div>
          }
        </form>
      </div>
    </div>
  `,
  styles: [`
    :host {
      display: flex;
      justify-content: center;
      align-items: center;
      min-height: 100vh;
      background-color: #f5f5f5; /* Or use a CSS variable if defined globally */
      padding: 1rem;
    }

    .register-form {
      width: 100%;
      max-width: 500px;
      padding: 2rem;
      background: white;
      border-radius: 8px;
      box-shadow: 0 0.5rem 1rem rgba(0, 0, 0, 0.15);
    }

    .form-row {
      display: flex;
      gap: 1rem;
    }

    .logo-link {
      display: flex;
      align-items: center;
      justify-content: center;
      text-decoration: none;
      color: var(--text-color-dark);
      gap: 0.75rem; /* Space between icon and text */
      transition: opacity 0.2s;
    }

    .logo-link:hover {
      opacity: 0.9;
    }

    .logo-icon {
      height: 96px;
      width: auto;
      margin: 1rem auto;
    }

    .logo h2 { margin: 0; font-size: 1.5rem; }

    /*
     * All styles for .form-group, label, .form-control, .is-invalid,
     * .invalid-feedback, and .btn are now inherited from the global
     * styles.scss and can be safely removed from this component.
     */

    .mt-3 {
      margin-top: 1rem;
    }

    .mr-1 {
      margin-right: 0.25rem;
    }
  `]
})
export class RegisterComponent {
  private authService = inject(AuthService);
  private router = inject(Router);
  private fb = inject(FormBuilder);
  private cdr = inject(ChangeDetectorRef);

  isLoading = false;
  error: string | null = null;

  registerForm = this.fb.group({
    firstName: ['', [Validators.required]],
    lastName: ['', [Validators.required]],
    email: ['', [Validators.required, Validators.email]],
    phone: [''],
    password: ['', [Validators.required, Validators.minLength(6)]],
    confirmPassword: ['', [Validators.required]],
    // FIX: Let TypeScript infer the type from the initial value.
    // The type will be correctly inferred as FormControl<File | null>.
    profilePicture: [null as File | null]
  }, { validators: passwordMatchValidator });

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (input.files && input.files.length > 0) {
      this.registerForm.patchValue({
        profilePicture: input.files[0]
      });
    }
  }

  onSubmit(): void {
    if (this.registerForm.invalid) {
      this.registerForm.markAllAsTouched();
      return;
    }

    this.isLoading = true;
    this.error = null;

    // FIX: Use getRawValue() to include all fields and manually construct
    // the payload to ensure it matches the RegisterUser model perfectly.
    const formValue = this.registerForm.getRawValue();

    const registerPayload: RegisterUser = {
      firstName: formValue.firstName || undefined,
      lastName: formValue.lastName || undefined,
      email: formValue.email || undefined,
      phone: formValue.phone || undefined,
      password: formValue.password || undefined,
      // The profilePicture can be null, which is fine.
      profilePicture: formValue.profilePicture || undefined
    };

    this.authService.register(registerPayload).subscribe({
      next: () => {
        this.isLoading = false;
        this.router.navigate(['/login'], { queryParams: { registered: 'true' } });
      },
      error: err => {
        this.isLoading = false;
        this.error = err.error?.message || 'Registration failed. Please try again.';
        this.cdr.markForCheck();
      }
    });
  }
}
