import { Component, inject, OnInit, ChangeDetectorRef, ViewChild } from '@angular/core';
import { FileListComponent } from '../../files/file-list/file-list.component';
import { FileUploadComponent } from '../../files/file-upload/file-upload.component';
import {
  AbstractControl,
  FormBuilder,
  ReactiveFormsModule,
  ValidationErrors,
  ValidatorFn,
  Validators
} from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { MeetingsService } from '../../../core/api/meetings.service';
import { CreateMeeting, Meeting, UpdateMeeting } from '../../../core/models/meeting.model';
import { Observable, tap, switchMap, of, map, concatMap, finalize } from 'rxjs';
import { AuthService } from '../../../core/api/auth.service';
import { AsyncPipe } from '@angular/common';
import { UserProfile } from '../../../core/models/user.model';
import { isApiErrorDetailedValidation } from '../../../core/models/error.model';

/**
 * Custom validator to ensure the end time is after the start time.
 * @returns A ValidatorFn that performs the check.
 */
export const endTimeAfterStartValidator: ValidatorFn = (
  control: AbstractControl
): ValidationErrors | null => {
  const startAt = control.get('startAt')?.value;
  const endAt = control.get('endAt')?.value;

  if (startAt && endAt) {
    const start = new Date(startAt);
    const end = new Date(endAt);
    return end <= start ? { endTimeAfterStart: true } : null;
  }
  return null;
};

// NEW: Define a View Model interface for the component's state
interface MeetingFormViewModel {
  isEditing: boolean;
  meeting?: Meeting;
  currentUser?: UserProfile;
}

@Component({
  selector: 'app-meeting-form',
  standalone: true,
  imports: [ReactiveFormsModule, FileListComponent, FileUploadComponent, AsyncPipe],
  template: `
    <!-- FIX: Use a single, unified @if block driven by the vm$ stream -->
    @if (vm$ | async; as vm) {
      <div class="meeting-form-container">
        <div class="header">
          <h2>{{ vm.isEditing ? 'Edit Meeting' : 'Create Meeting' }}</h2>
        </div>

        <form [formGroup]="meetingForm" (ngSubmit)="onSubmit(vm)" class="meeting-form">
        <!-- ... other form fields are fine ... -->
        <div class="form-group">
          <label for="name">Meeting Name</label>
          <input
            type="text"
            id="name"
            formControlName="name"
            class="form-control"
            [class.is-invalid]="meetingForm.get('name')?.invalid && meetingForm.get('name')?.touched"
          />
          <!-- FIX: Use modern @if syntax -->
          @if (meetingForm.get('name')?.invalid && meetingForm.get('name')?.touched) {
            <div class="invalid-feedback">
              @if (meetingForm.get('name')?.errors?.['required']) {
                <div>Meeting name is required</div>
              }
              @if (meetingForm.get('name')?.errors?.['serverError']) {
                <div>{{ meetingForm.get('name')?.errors?.['serverError'] }}</div>
              }
            </div>
          }
        </div>

        <div class="form-group">
          <label for="description">Description</label>
          <textarea
            id="description"
            formControlName="description"
            class="form-control"
            rows="4"
          [class.is-invalid]="meetingForm.get('description')?.invalid && meetingForm.get('description')?.touched"
          ></textarea>
          <!-- FIX: Use modern @if syntax -->
          @if (meetingForm.get('description')?.invalid && meetingForm.get('description')?.touched) {
            <div class="invalid-feedback">
              @if (meetingForm.get('description')?.errors?.['required']) {
                <div>Meeting description is required</div>
              }
              @if (meetingForm.get('description')?.errors?.['serverError']) {
                <div>{{ meetingForm.get('description')?.errors?.['serverError'] }}</div>
              }
            </div>
          }
        </div>

        <div class="form-row">
          <div class="form-group">
            <label for="startAt">Start Time</label>
            <input
              type="datetime-local"
              id="startAt"
              formControlName="startAt"
              class="form-control"
              [class.is-invalid]="
                meetingForm.get('startAt')?.invalid && meetingForm.get('startAt')?.touched
              "
            />
            @if (meetingForm.get('startAt')?.invalid && meetingForm.get('startAt')?.touched) {
              <div class="invalid-feedback">
                @if (meetingForm.get('startAt')?.errors?.['required']) {
                  <div>Start time is required</div>
                }
                @if (meetingForm.get('startAt')?.errors?.['serverError']) {
                  <div>{{ meetingForm.get('startAt')?.errors?.['serverError'] }}</div>
                }
              </div>
            }
          </div>

          <div class="form-group">
            <label for="endAt">End Time</label>
            <input
              type="datetime-local"
              id="endAt"
              formControlName="endAt"
              class="form-control"
              [class.is-invalid]="
                meetingForm.get('endAt')?.invalid && meetingForm.get('endAt')?.touched
              "
            />
            @if (meetingForm.get('endAt')?.invalid && meetingForm.get('endAt')?.touched) {
              <div class="invalid-feedback">
                @if (meetingForm.get('endAt')?.errors?.['required']) {
                  <div>End time is required</div>
                }
                <!-- FIX: Corrected the error key (no leading space) -->
                @if (meetingForm.get('endAt')?.errors?.['endTimeAfterStart']) {
                  <div>End time must be after start time</div>
                }
                @if (meetingForm.get('endAt')?.errors?.['serverError']) {
                  <div>{{ meetingForm.get('endAt')?.errors?.['serverError'] }}</div>
                }
              </div>
            }
          </div>
        </div>

        <div class="form-group">
          <label for="participantEmails">Participant Emails (comma separated)</label>
          <input
            type="text"
            id="participantEmails"
            formControlName="participantEmails"
            class="form-control"
            placeholder="user1@example.com, user2@example.com"
            [class.is-invalid]="meetingForm.get('participantEmails')?.invalid && meetingForm.get('participantEmails')?.touched"
          />
          <!-- FIX: Use modern @if syntax -->
          @if (meetingForm.get('participantEmails')?.invalid && meetingForm.get('participantEmails')?.touched) {
            <div class="invalid-feedback">
              @if (meetingForm.get('participantEmails')?.errors?.['required']) {
                <div>Meeting participants is required</div>
              }
              @if (meetingForm.get('participantEmails')?.errors?.['serverError']) {
                <div>{{ meetingForm.get('participantEmails')?.errors?.['serverError'] }}</div>
              }
            </div>
          }
          <small class="form-text">Enter participant emails separated by commas</small>
        </div>

          <div class="files-section">
            <!-- This now renders correctly when vm.isEditing is true -->
            @if (vm.isEditing && vm.meeting && vm.currentUser) {
              <app-file-list
                [meetingId]="vm.meeting.id"
                [organizerId]="vm.meeting.organizerId"
                [canDeleteFiles]="true"
              ></app-file-list>
            }

            <h4>Upload attachments</h4>
            <app-file-upload
              [meetingId]="vm.meeting?.id!"
              (uploadSuccess)="refreshFileList()">
            </app-file-upload>
          </div>

          <div class="form-actions">
            <button type="button" class="btn btn-outline" (click)="cancel(vm)">Cancel</button>
            <button type="submit" class="btn btn-primary" [disabled]="meetingForm.invalid || isLoading">
              @if (isLoading) {
                <span class="spinner-border spinner-border-sm mr-1"></span>
              }
              {{ vm.isEditing ? 'Update Meeting' : 'Create Meeting' }}
            </button>
          </div>

          @if (error) {
            <div class="alert alert-danger mt-3">{{ error }}</div>
          }
        </form>
      </div>
    } @else {
      <div class="loading">Loading form...</div>
    }
  `,
  styles: [`
    :host {
      display: block;
    }

    .header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 2rem; /* var(--spacing-lg) */
    }

    .header h2 {
      margin: 0;
      color: var(--text-color-dark, #333);
    }

    .meeting-form {
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

    .form-text {
      display: block;
      margin-top: 0.25rem;
      color: var(--text-color-muted, #6c757d);
      font-size: 0.875rem; /* var(--font-size-sm) */
    }

    .form-actions {
      display: flex;
      justify-content: flex-end;
      gap: 1rem; /* var(--spacing-base) */
      margin-top: 2rem; /* var(--spacing-lg) */
    }

    .mt-3 {
      margin-top: 1rem; /* var(--spacing-base) */
    }

    .mr-1 {
      margin-right: 0.25rem;
    }

    /* Responsive adjustment for the form row */
    @media (max-width: 768px) {
      .form-row {
        grid-template-columns: 1fr; /* Stack to a single column */
      }
    }

    /*
     * All styles for .form-group, label, .form-control, .is-invalid,
     * .invalid-feedback, .btn, .alert, and their variants have been removed
     * as they are now provided by the global styles.scss.
     */
  `]
})
export class MeetingFormComponent implements OnInit {
  @ViewChild(FileUploadComponent) private fileUploadComponent?: FileUploadComponent;
  @ViewChild(FileListComponent) private fileListComponent?: FileListComponent;

  private authService = inject(AuthService);
  private meetingsService = inject(MeetingsService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private fb = inject(FormBuilder);
  private cdr = inject(ChangeDetectorRef);

  isLoading = false;
  error: string | null = null;

  meetingForm = this.fb.group({
    name: ['', [Validators.required]],
    description: [''],
    startAt: ['', [Validators.required]],
    endAt: ['', [Validators.required]],
    participantEmails: ['']
  }, { validators: endTimeAfterStartValidator });

  vm$!: Observable<MeetingFormViewModel>;

  ngOnInit() {
    this.vm$ = this.route.paramMap.pipe(
      switchMap(params => {
        const id = params.get('id');
        const currentUser = this.authService.getCurrentUser();

        if (!currentUser) {
          this.router.navigate(['/login']);
          return of({ isEditing: false, error: 'User not found' } as MeetingFormViewModel);
        }

        // --- EDIT MODE ---
        if (id) {
          return this.meetingsService.getMeetingById(id).pipe(
            tap(meeting => this.patchForm(meeting)), // Side effect to patch form
            map(meeting => ({
              isEditing: true,
              meeting: meeting,
              currentUser: { id: currentUser.id } as UserProfile
            }))
          );
        }

        // --- CREATE MODE ---
        return of({
          isEditing: false,
          currentUser: { id: currentUser.id } as UserProfile
        });
      })
    );
  }

  private patchForm(meeting: Meeting): void {
    this.meetingForm.patchValue({
      name: meeting.name,
      description: meeting.description,
      startAt: this.formatDateTimeForInput(meeting.startAt),
      endAt: this.formatDateTimeForInput(meeting.endAt),
      participantEmails: meeting.participants?.map(p => p.email).join(', ') || ''
    });
  }

  private formatDateTimeForInput(dateString: string): string {
    // The API provides a UTC date string (e.g., ending in 'Z').
    // new Date() correctly parses this into a Date object representing that point in time.
    // The getFullYear(), getHours(), etc. methods then automatically return the date/time
    // components in the browser's local timezone, which is what the datetime-local input requires.
    const date = new Date(dateString);
    const year = date.getFullYear();
    const month = String(date.getMonth() + 1).padStart(2, '0');
    const day = String(date.getDate()).padStart(2, '0');
    const hours = String(date.getHours()).padStart(2, '0');
    const minutes = String(date.getMinutes()).padStart(2, '0');
    return `${year}-${month}-${day}T${hours}:${minutes}`;
  }

  onSubmit(vm: MeetingFormViewModel): void {
    if (this.meetingForm.invalid) {
      this.meetingForm.markAllAsTouched();
      return;
    }

    this.isLoading = true;
    this.error = null;

    const formValue = this.meetingForm.value;
    const participantEmails = formValue.participantEmails?.split(',').map(e => e.trim()).filter(e => !!e) || [];

    let save$: Observable<Meeting>;

    if (vm.isEditing && vm.meeting) {
      const payload: UpdateMeeting = {
        name: formValue.name!,
        description: formValue.description!,
        // The form value is a string like 'YYYY-MM-DDTHH:mm', which new Date() interprets as local time.
        // .toISOString() then correctly converts this local time to its UTC equivalent string for the API.
        startAt: new Date(formValue.startAt!).toISOString(),
        endAt: new Date(formValue.endAt!).toISOString(),
        participantEmails: participantEmails
      };
      save$ = this.meetingsService.updateMeeting(vm.meeting.id, payload);
    } else {
      const payload: CreateMeeting = {
        name: formValue.name!,
        description: formValue.description!,
        startAt: new Date(formValue.startAt!).toISOString(),
        endAt: new Date(formValue.endAt!).toISOString(),
        participantEmails: participantEmails
      };
      save$ = this.meetingsService.createMeeting(payload);
    }

    const hadFiles = (this.fileUploadComponent?.selectedFiles?.length ?? 0) > 0;

    save$.pipe(
      concatMap(savedMeeting => {
        // After saving, update the meetingId for the uploader
        if (this.fileUploadComponent) {
          this.fileUploadComponent.meetingId = savedMeeting.id;
        }

        if (hadFiles) {
          const upload$ = this.fileUploadComponent?.uploadFiles();
          return upload$ ? upload$.pipe(map(() => savedMeeting)) : of(savedMeeting);
        }
        return of(savedMeeting);
      }),
      finalize(() => {
        this.isLoading = false;
        this.cdr.markForCheck();
      })
    ).subscribe({
      next: (savedMeeting: Meeting) => {
        // Adjust navigation based on mode
        if (vm.isEditing) {
          // Refresh state on the same page
          this.refreshFileList();
          const id = vm.meeting?.id || savedMeeting.id;
          if (id) {
            this.meetingsService.getMeetingById(id).subscribe(m => this.patchForm(m));
          }
        } else {
          // Create mode: go to edit page of the newly created meeting
          this.router.navigate(['/meetings', savedMeeting.id, 'edit']);
        }
      },
      error: (err: any) => { // <-- Use 'any' to handle both Error and ApiisApiErrorDetailedValidation

        // --- NEW ERROR HANDLING LOGIC ---
        if (isApiErrorDetailedValidation(err)) {
          // This is a structured validation error from the API
          this.applyValidationErrors(err.errors);
        } else {
          // This is a simple string error from our interceptor
          this.error = err.message || 'An unexpected error occurred.';
        }
      }
    });
  }

  private applyValidationErrors(errors: { [key: string]: string[] }): void {
    for (const key in errors) {
      if (errors.hasOwnProperty(key)) {
        // The API keys (e.g., "StartAt") are PascalCase. Our form controls are camelCase.
        const formControlName = key.charAt(0).toLowerCase() + key.slice(1);

        const control = this.meetingForm.get(formControlName);
        if (control) {
          // Get the first error message for that field
          const errorMessage = errors[key][0];
          control.setErrors({ serverError: errorMessage });
          this.error = errorMessage;
        } else {
          // If the error key doesn't match a form control, show it as a general error.
          this.error = `An error occurred with the '${key}' field.`;
        }
      }
    }
    this.meetingForm.markAllAsTouched(); // Ensure the errors are visible
  }

  refreshFileList(): void {
    this.fileListComponent?.refresh();
  }

  cancel(vm: MeetingFormViewModel): void {
    const backUrl = vm.isEditing && vm.meeting ? ['/meetings', vm.meeting.id] : ['/meetings'];
    this.router.navigate(backUrl);
  }
}
