import { Component, inject } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { FileListComponent } from '../../files/file-list/file-list.component';
import { UserProfileService } from '../../../core/api/user-profile.service';
import { UserProfile } from '../../../core/models/user.model';
import { MeetingsService } from '../../../core/api/meetings.service';
import { Meeting } from '../../../core/models/meeting.model';
import { AsyncPipe, DatePipe, LowerCasePipe, NgClass } from '@angular/common';
import { Observable, switchMap } from 'rxjs';
import { MeetingStatusPipe } from '../../../shared/pipes/meeting-status-pipe';

@Component({
  selector: 'app-meeting-detail',
  standalone: true,
  // FIX: Remove unused RouterLink. CommonModule is not needed with @if/@for.
  imports: [AsyncPipe, DatePipe, FileListComponent, MeetingStatusPipe, NgClass, LowerCasePipe],//, NgClass, LowerCasePipe, MeetingStatusPipe, FileUploadComponent
  template: `
    <div class="meeting-detail-container">
      @if (currentUser$ | async; as currentUser) {
        <!-- FIX: Use modern @if/@else control flow -->
        @if (meeting$ | async; as meeting) {
          <div class="header">
            <h2>Meeting Details</h2>
            <div class="header-actions">
              <button class="btn btn-outline" (click)="goBack()">Back to Meetings</button>
              <button class="btn btn-primary" (click)="editMeeting(meeting.id)">Edit Meeting</button>
            </div>
          </div>

          <div class="meeting-content">
            <div class="meeting-info">
              <div class="meeting-header">
                <h3>{{ meeting.name }}</h3>
                <span
                  class="status"
                  [ngClass]="(meeting | meetingStatus | lowercase).replace(' ', '-')"
                >
                  {{ meeting | meetingStatus }}
                </span>
              </div>

              <div class="info-grid">
                <div class="info-item">
                  <label>Description:</label>
                  <p>{{ meeting.description || 'N/A' }}</p>
                </div>

                <div class="info-item">
                  <label>Start Time:</label>
                  <p>{{ meeting.startAt | date: 'medium' }}</p>
                </div>

                <div class="info-item">
                  <label>End Time:</label>
                  <p>{{ meeting.endAt | date: 'medium' }}</p>
                </div>

                <div class="info-item">
                  <label>Status:</label>
                  <span class="status" [class.canceled]="meeting.isCanceled">
                    {{ meeting.isCanceled ? 'Canceled' : 'Scheduled' }}
                  </span>
                </div>

                <div class="info-item">
                  <label>Organizer:</label>
                  <p>{{ meeting.organizerId }}</p>
                </div>
              </div>
            </div>

            <div class="participants-section">
              <h4>Participants ({{ meeting.participants?.length || 0 }})</h4>
              <!-- FIX: Use @if/@else and @for with track -->
              @if (meeting.participants && meeting.participants.length > 0) {
                <div class="participants-list">
                  @for (participant of meeting.participants; track participant.userId) {
                    <div class="participant-item">
                      <span class="participant-name">{{ participant.firstName }} {{ participant.lastName }}</span>
                      <span class="participant-email">{{ participant.email }}</span>
                    </div>
                  }
                </div>
              } @else {
                <p class="no-participants">No participants added to this meeting.</p>
              }
            </div>

            <!-- NEW: Add the file upload and file list components -->
            <div class="files-section">
              <app-file-list
                [meetingId]="meeting.id"
                [organizerId]="meeting.organizerId"
                [canDeleteFiles]="false"
              ></app-file-list>
            </div>

            <div class="meeting-actions">
              <button class="btn btn-danger" (click)="cancelMeeting(meeting.id)">Cancel Meeting</button>
            </div>
          </div>
        } @else {
          <div class="loading">Loading meeting details ⌛</div>
        }
      } @else {
        <div class="loading">Loading current user ⌛</div>
      }
    </div>
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

    .header-actions {
      display: flex;
      gap: 1rem; /* var(--spacing-base) */
    }

    .meeting-content {
      background: var(--background-color-white, white);
      border-radius: 8px;
      padding: 2rem; /* var(--spacing-lg) */
      box-shadow: var(--box-shadow, 0 2px 8px rgba(0,0,0,0.1));
    }

    .meeting-info h3 {
      margin-top: 0;
      margin-bottom: 1.5rem;
      color: var(--text-color-dark, #333);
      padding: var(--spacing-sm);
      background-color: var(--card-border-color);
      border-radius: var(--border-radius);
    }

    .meeting-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: var(--spacing-base);
      padding: var(--spacing-sm);
      background-color: var(--card-border-color);
      border-radius: var(--border-radius);
    }

    .meeting-header h3 {
      margin: 0;
      color: var(--text-color-dark, #333);
    }

    .info-grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(250px, 1fr));
      gap: 1.5rem;
      margin-bottom: var(--spacing-lg);
      padding: var(--spacing-sm);
    }

    .info-item label {
      font-weight: bold;
      margin-bottom: 0.25rem;
      color: var(--text-color-light, #555);
    }

    .info-item p,
    .info-item span {
      margin: 0;
      color: var(--text-color-muted, #666);
    }

    // .status {
    //   display: inline-block;
    //   padding: 0.25rem 0.5rem;
    //   border-radius: 12px;
    //   font-size: 0.75rem;
    //   font-weight: bold;
    //   align-self: flex-start;
    // }

    // .status.canceled {
    //   background-color: var(--danger-color, #dc3545);
    //   color: var(--text-color-white, #fff);
    // }

    .participants-section h4 {
      margin-top: 0;
      margin-bottom: 1rem; /* var(--spacing-base) */
      color: var(--text-color-dark, #333);
      padding: var(--spacing-sm);
      background-color: var(--card-border-color);
      border-radius: var(--border-radius);
    }

    .participant-item {
      display: flex;
      align-items: center;
      padding: 0.75rem 0;
    }

    .participant-item::before {
      content: '•';
      line-height: 1;
      margin-right: 0.75rem;
    }

    .participant-name {
      margin-right: auto;
      font-weight: 500;
      color: var(--text-color-dark, #333);
    }

    .participant-email {
      color: var(--text-color-muted, #666);
    }

    .no-participants {
      color: var(--text-color-muted, #666);
      font-style: italic;
      text-align: center;
      padding: 1rem; /* var(--spacing-base) */
    }

    .meeting-actions {
      display: flex;
      justify-content: flex-end;
      margin-top: 2rem; /* var(--spacing-lg) */
      padding-top: 1.5rem;
      // border-top: 1px solid var(--card-border-color, #eee);
    }

    .loading {
      text-align: center;
      padding: 2rem; /* var(--spacing-lg) */
      color: var(--text-color-muted, #666);
    }

    /*
     * All styles for .btn and its variants have been removed
     * as they are now provided by the global styles.scss.
     */
  `]
})
export class MeetingDetailComponent {
  private userProfileService = inject(UserProfileService);
  private meetingsService = inject(MeetingsService);
  private route = inject(ActivatedRoute);
  private router = inject(Router);

  // Add a stream for the current user's profile
  currentUser$: Observable<UserProfile> = this.userProfileService.getMe();

  // FIX: Create a single, declarative data stream derived from the route.
  // This eliminates the need for ngOnInit, loadMeeting(), and the meetingId property.
  meeting$: Observable<Meeting> = this.route.paramMap.pipe(
    switchMap(params => {
      const meetingId = params.get('id')!;
      return this.meetingsService.getMeetingById(meetingId);
    })
  );

  goBack(): void {
    this.router.navigate(['/meetings']);
  }

  editMeeting(id: string): void {
    this.router.navigate(['/meetings', id, 'edit']);
  }

  cancelMeeting(id: string): void {
    if (confirm('Are you sure you want to cancel this meeting?')) {
      this.meetingsService.cancelMeeting(id).subscribe({
        complete: () => {
          this.router.navigate(['/meetings']);
        }
      });
    }
  }
}
