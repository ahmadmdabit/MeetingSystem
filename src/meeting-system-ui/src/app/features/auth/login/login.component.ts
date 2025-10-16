import { Component, inject, OnInit, ChangeDetectorRef } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { AuthService } from '../../../core/api/auth.service';
import { Login } from '../../../core/models/auth.model';

@Component({
  selector: 'app-login',
  standalone: true,
  // FIX: Use @if, so NgIf is no longer needed.
  imports: [ReactiveFormsModule, RouterLink],
  template: `
    <div class="login-container">
      <a routerLink="/dashboard" class="logo-link">
        <img src="assets/images/meeting-system.png" alt="Meeting System Logo" class="logo-icon" />
      </a>
      <div class="login-form">
        <h2>Login</h2>
        <!-- Add a success message for users redirected from registration -->
        @if (showSuccessMessage) {
          <div class="alert alert-success">Registration successful! Please log in.</div>
        }

        <form [formGroup]="loginForm" (ngSubmit)="onSubmit()">
          <div class="form-group">
            <label for="email">Email</label>
            <input
              type="email"
              id="email"
              formControlName="email"
              class="form-control"
              [class.is-invalid]="loginForm.get('email')?.invalid && loginForm.get('email')?.touched"
            />
            <!-- FIX: Use modern @if syntax -->
            @if (loginForm.get('email')?.invalid && loginForm.get('email')?.touched) {
              <div class="invalid-feedback">
                @if (loginForm.get('email')?.errors?.['required']) {
                  <div>Email is required</div>
                }
                @if (loginForm.get('email')?.errors?.['email']) {
                  <div>Please enter a valid email</div>
                }
              </div>
            }
          </div>

          <div class="form-group">
            <label for="password">Password</label>
            <input
              type="password"
              id="password"
              formControlName="password"
              class="form-control"
              [class.is-invalid]="
                loginForm.get('password')?.invalid && loginForm.get('password')?.touched
              "
            />
            @if (loginForm.get('password')?.invalid && loginForm.get('password')?.touched) {
              <div class="invalid-feedback">
                @if (loginForm.get('password')?.errors?.['required']) {
                  <div>Password is required</div>
                }
              </div>
            }
          </div>

          <button type="submit" class="btn btn-primary" [disabled]="loginForm.invalid || isLoading">
            @if (isLoading) {
              <span class="spinner-border spinner-border-sm mr-1"></span>
            }
            Login
          </button>

          <div class="mt-3">
            <p>Don't have an account? <a routerLink="/register">Register here</a></p>
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
      padding: 1rem;
    }

    .login-form {
      width: 100%;
      max-width: 400px;
      padding: 2rem;
      background: var(--background-color-white, white); /* Use CSS var with fallback */
      border-radius: var(--border-radius, 0.375rem);
      box-shadow: var(--box-shadow, 0 0.5rem 1rem rgba(0, 0, 0, 0.15));
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

    .mt-3 {
      margin-top: 1rem;
    }
  `]
})
export class LoginComponent implements OnInit {
  private authService = inject(AuthService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private fb = inject(FormBuilder);
  private cdr = inject(ChangeDetectorRef);

  isLoading = false;
  error: string | null = null;
  showSuccessMessage = false;

  loginForm = this.fb.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required]]
  });

  ngOnInit(): void {
    // Check for the 'registered' query param to show a success message
    if (this.route.snapshot.queryParamMap.get('registered') === 'true') {
      this.showSuccessMessage = true;
    }
  }

  onSubmit(): void {
    if (this.loginForm.invalid) {
      this.loginForm.markAllAsTouched();
      return;
    }

    this.isLoading = true;
    this.error = null;

    // FIX: Manually construct the payload to ensure type safety.
    // Convert any null values from the form to undefined.
    const payload: Login = {
      email: this.loginForm.value.email || undefined,
      password: this.loginForm.value.password || undefined
    };

    this.authService.login(payload).subscribe({
      next: () => {
        this.isLoading = false;
        // Navigate to a dashboard or home page after successful login
        this.router.navigate(['/dashboard']);
      },
      error: err => {
        this.isLoading = false;
        this.error = err.error?.message || 'Login failed. Please check your credentials.';
        this.cdr.markForCheck();
      }
    });
  }
}
