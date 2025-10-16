import {
  Component,
  inject,
  OnInit,
  ChangeDetectionStrategy,
  ChangeDetectorRef
} from '@angular/core';
import { UserProfileService } from '../../core/api/user-profile.service';
import { UpdateUserProfile, UserProfile } from '../../core/models/user.model';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { AsyncPipe } from '@angular/common';
import { Observable, tap } from 'rxjs';

@Component({
  selector: 'app-user-profile',
  standalone: true,
  // FIX: Remove CommonModule, as @if is built-in.
  imports: [ReactiveFormsModule, AsyncPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="user-profile-container">
      <div class="header">
        <h2>User Profile</h2>
      </div>

      <!-- FIX: Use modern @if/@else control flow -->
      @if (userProfile$ | async; as profile) {
        <div class="profile-content">
          <form [formGroup]="profileForm" (ngSubmit)="onSubmit()" class="profile-form">
            <div class="form-row">
              <div class="form-group">
                <label for="firstName">First Name</label>
                <input
                  type="text"
                  id="firstName"
                  formControlName="firstName"
                  class="form-control"
                  [class.is-invalid]="
                    profileForm.get('firstName')?.invalid && profileForm.get('firstName')?.touched
                  "
                />
                <!-- FIX: Use modern @if for error messages -->
                @if (
                  profileForm.get('firstName')?.invalid && profileForm.get('firstName')?.touched
                ) {
                  <div class="invalid-feedback">
                    @if (profileForm.get('firstName')?.errors?.['required']) {
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
                    profileForm.get('lastName')?.invalid && profileForm.get('lastName')?.touched
                  "
                />
                @if (profileForm.get('lastName')?.invalid && profileForm.get('lastName')?.touched) {
                  <div class="invalid-feedback">
                    @if (profileForm.get('lastName')?.errors?.['required']) {
                      <div>Last name is required</div>
                    }
                  </div>
                }
              </div>
            </div>

            <div class="form-group">
              <label for="email">Email</label>
              <input type="email" id="email" formControlName="email" class="form-control" readonly />
            </div>

            <div class="form-group">
              <label for="phone">Phone</label>
              <input type="tel" id="phone" formControlName="phone" class="form-control" />
            </div>

            <div class="form-actions">
              <button type="submit" class="btn btn-primary" [disabled]="profileForm.pristine || profileForm.invalid || isLoading">
                @if (isLoading) {
                  <span class="spinner-border spinner-border-sm mr-1"></span>
                }
                Update Profile
              </button>
            </div>

            @if (error) {
              <div class="alert alert-danger mt-3">{{ error }}</div>
            }
          </form>

          <div class="profile-picture-section">
            <h3>Profile Picture</h3>
            <div class="picture-container">
              @if (profile.profilePictureUrl; as picUrl) {
                <img [src]="picUrl" alt="Profile Picture" class="profile-image" />
              } @else {
                <div class="no-picture-placeholder">No Profile Picture</div>
              }
            </div>

            <div class="picture-actions">
              <input
                type="file"
                #fileInput
                (change)="onFileSelected($event)"
                accept="image/*"
                style="display: none"
              />
              <button class="btn btn-outline" (click)="fileInput.click()">Upload Picture</button>
              @if (profile.profilePictureUrl) {
                <button class="btn btn-danger" (click)="deletePicture()">Delete Picture</button>
              }
            </div>

            @if (uploadError) {
              <div class="alert alert-danger mt-3">{{ uploadError }}</div>
            }
          </div>
        </div>
      } @else {
        <div class="loading">Loading profile ⌛</div>
      }
    </div>
  `,
  styles: [`
    :host {
      display: block;
    }

    .header {
      margin-bottom: 2rem; /* var(--spacing-lg) */
    }

    .header h2 {
      margin: 0;
      color: var(--text-color-dark, #333);
    }

    .profile-content {
      display: grid;
      grid-template-columns: 2fr 1fr;
      gap: 2rem; /* var(--spacing-lg) */
      background: var(--background-color-white, white);
      border-radius: 8px;
      padding: 2rem; /* var(--spacing-lg) */
      box-shadow: var(--box-shadow, 0 2px 8px rgba(0,0,0,0.1));
    }

    .form-row {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 1.5rem;
    }

    .form-actions {
      margin-top: 1rem; /* var(--spacing-base) */
    }

    .profile-picture-section {
      // border-left: 1px solid var(--card-border-color, #eee);
      padding-left: 2rem; /* var(--spacing-lg) */
    }

    .profile-picture-section h3 {
      margin-top: 0;
      color: var(--text-color-dark, #333);
    }

    .picture-container {
      text-align: center;
      margin-bottom: 1rem; /* var(--spacing-base) */
    }

    .profile-image {
      width: 150px;
      height: 150px;
      border-radius: 50%;
      object-fit: cover;
      border: 3px solid var(--card-border-color, #eee);
    }

    .no-picture-placeholder {
      width: 150px;
      height: 150px;
      border-radius: 50%;
      background-color: var(--background-color-light, #f8f9fa);
      display: flex;
      align-items: center;
      justify-content: center;
      margin: 0 auto;
      border: 3px solid var(--card-border-color, #eee);
      color: var(--text-color-muted, #666);
    }

    .picture-actions {
      display: flex;
      flex-direction: column;
      gap: 0.5rem; /* var(--spacing-sm) */
    }

    .loading {
      text-align: center;
      padding: 2rem; /* var(--spacing-lg) */
      color: var(--text-color-muted, #666);
    }

    .mt-3 {
      margin-top: 1rem; /* var(--spacing-base) */
    }

    .mr-1 {
      margin-right: 0.25rem;
    }

    /* --- Responsive Adjustments --- */
    @media (max-width: 992px) {
      .profile-content {
        grid-template-columns: 1fr; /* Stack to a single column */
      }
      .profile-picture-section {
        border-left: none;
        padding-left: 0;
        // border-top: 1px solid var(--card-border-color, #eee);
        padding-top: 2rem; /* var(--spacing-lg) */
      }
    }

    @media (max-width: 576px) {
      .form-row {
        grid-template-columns: 1fr; /* Stack first/last name on small screens */
      }
    }

    /*
     * All styles for .form-group, label, .form-control, .is-invalid,
     * .invalid-feedback, .btn, .alert, and their variants have been removed
     * as they are now provided by the global styles.scss.
     */
  `]
})
export class UserProfileComponent implements OnInit {
  private userProfileService = inject(UserProfileService);
  private fb = inject(FormBuilder);
  // FIX: Inject ChangeDetectorRef for OnPush components
  private cdr = inject(ChangeDetectorRef);

  userProfile$!: Observable<UserProfile>;
  isLoading = false;
  error: string | null = null;
  uploadError: string | null = null;

  // Note: The profilePictureUrl is now part of the UserProfile model stream
  // and doesn't need to be a separate state property.

  profileForm = this.fb.group({
    firstName: ['', [Validators.required]],
    lastName: ['', [Validators.required]],
    // Email is disabled to prevent submission and make it clear it's read-only
    email: [{ value: '', disabled: true }, [Validators.required]],
    phone: ['']
  });

  ngOnInit() {
    this.loadUserProfile();
  }

  loadUserProfile() {
    // FIX: Use a single stream and tap into it to perform the side effect of patching the form.
    this.userProfile$ = this.userProfileService.getMe().pipe(
      tap(profile => {
        // Use reset instead of patchValue to also update the pristine status of the form
        this.profileForm.reset(profile);
      })
    );
  }

  onSubmit() {
    if (this.profileForm.invalid) {
      return;
    }

    this.isLoading = true;
    this.error = null;

    // FIX: Use getRawValue() to include disabled controls like email if needed,
    // but here we only need the editable values.
    const formValue = this.profileForm.value;

    // FIX: Ensure the payload matches the UpdateUserProfile type by handling potential nulls.
    const payload: UpdateUserProfile = {
      firstName: formValue.firstName || undefined,
      lastName: formValue.lastName || undefined,
      phone: formValue.phone || undefined
    };

    this.userProfileService.updateMe(payload).subscribe({
      next: () => {
        this.isLoading = false;
        // Mark the form as pristine again after a successful save
        this.profileForm.markAsPristine();
        this.cdr.markForCheck(); // Trigger change detection
      },
      error: err => {
        this.isLoading = false;
        this.error = err.error?.message || 'Failed to update profile';
        this.cdr.markForCheck(); // Trigger change detection
      }
    });
  }

  onFileSelected(event: Event) {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (file) {
      this.uploadPicture(file);
    }
  }

  uploadPicture(file: File) {
    this.uploadError = null;
    this.userProfileService.uploadProfilePicture(file).subscribe({
      next: () => {
        // Reload profile to update the picture
        this.loadUserProfile();
        // We need to manually trigger change detection because this is an async callback
        this.cdr.markForCheck();
      },
      error: err => {
        this.uploadError = err.error?.message || 'Failed to upload picture';
        this.cdr.markForCheck();
      }
    });
  }

  deletePicture() {
    this.uploadError = null;
    this.userProfileService.deleteProfilePicture().subscribe({
      next: () => {
        this.loadUserProfile();
        this.cdr.markForCheck();
      },
      error: err => {
        this.uploadError = err.error?.message || 'Failed to delete picture';
        this.cdr.markForCheck();
      }
    });
  }
}
