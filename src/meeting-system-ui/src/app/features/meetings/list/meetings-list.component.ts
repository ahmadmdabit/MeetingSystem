import { Component, inject, ChangeDetectionStrategy, Pipe, PipeTransform } from '@angular/core';
import { Router } from '@angular/router';
import { MeetingsService } from '../../../core/api/meetings.service';
import { Meeting } from '../../../core/models/meeting.model';
import { AsyncPipe, DatePipe, NgClass, LowerCasePipe, TitleCasePipe } from '@angular/common';
import { Observable, BehaviorSubject, switchMap, map } from 'rxjs';
import { MeetingStatusPipe } from '../../../shared/pipes/meeting-status-pipe';

// NEW: Define a structured object for the categorized meetings
interface CategorizedMeetings {
  upcoming: Meeting[];
  inProgress: Meeting[];
  finished: Meeting[];
  canceled: Meeting[];
}

// NEW: Define the keys for iteration in the template
export type MeetingCategory = keyof CategorizedMeetings;

@Component({
  selector: 'app-meetings-list',
  standalone: true,
  imports: [AsyncPipe, DatePipe, TitleCasePipe, NgClass, LowerCasePipe, MeetingStatusPipe],
  // NEW: Add the MeetingStatusPipe to the providers array
  providers: [MeetingStatusPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="meetings-list-container">
      <div class="header">
        <h2>Meetings</h2>
        <button class="btn btn-primary" (click)="createMeeting()">Create Meeting</button>
      </div>

      <!-- FIX: The async pipe now unwraps the categorized object -->
      @if (categorizedMeetings$ | async; as categorized) {
        <!-- NEW: Iterate over the defined categories to render sections -->
        @for (category of meetingCategories; track category) {
          <!-- Only show the section if it has meetings -->
          @if (categorized[category].length > 0) {
            <div class="category-section">
              <h3 class="category-title">{{ category | titlecase }}</h3>
              <div class="meetings-grid">
                <!-- Loop through the meetings in the current category -->
                @for (meeting of categorized[category]; track meeting.id) {
                  <div class="meeting-card">
                    <div class="meeting-header">
                      <h3>{{ meeting.name }}</h3>
                      <span
                        class="status"
                        [ngClass]="(meeting | meetingStatus | lowercase).replace(' ', '-')"
                      >
                        {{ meeting | meetingStatus }}
                      </span>
                    </div>

                    <div class="meeting-details">
                      <p><strong>Description:</strong> {{ meeting.description || 'N/A' }}</p>
                      <!-- The DatePipe automatically converts the UTC date string from the API to the user's local browser time. -->
                      <p><strong>Start:</strong> {{ meeting.startAt | date: 'medium' : 'Europe/Istanbul' }}</p>
                      <p><strong>End:</strong> {{ meeting.endAt | date: 'medium' : 'Europe/Istanbul' }}</p>
                      <p><strong>Participants:</strong> {{ meeting.participants?.length || 0 }}</p>
                    </div>

                    <div class="meeting-actions">
                      <button class="btn btn-outline" (click)="viewMeeting(meeting.id)">View</button>
                      <button class="btn btn-outline" (click)="editMeeting(meeting.id)">Edit</button>
                      <button class="btn btn-danger" (click)="cancelMeeting(meeting.id)">Cancel</button>
                    </div>
                  </div>
                }
              </div>
            </div>
          }
        }
      } @else {
        <div class="loading">Loading meetings ⌛</div>
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

    /* NEW: Styles for the category sections */
    .category-section {
      margin-bottom: 2.5rem;
    }

    .category-title {
      font-size: 1.5rem;
      color: var(--text-color-white, #fff);
      margin-bottom: 1rem;
      padding-bottom: 0.5rem;
      padding: var(--spacing-sm);
      background-color: var(--primary-color);
      border-radius: var(--border-radius);
    }

    .meetings-grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(300px, 1fr));
      gap: 1.5rem;
    }

    .meeting-card {
      display: flex;
      flex-direction: column;
      background: var(--background-color-white, white);
      border-radius: 8px;
      box-shadow: var(--box-shadow, 0 2px 8px rgba(0,0,0,0.1));
      padding: 1.5rem;
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

    .meeting-details {
      flex-grow: 1; /* Ensures this section fills available space */
      padding: var(--spacing-sm);
    }

    .meeting-details p {
      margin: 0.5rem 0; /* var(--spacing-sm) 0 */
      color: var(--text-color-muted, #666);
    }

    .meeting-details strong {
      color: var(--text-color-dark, #333);
    }

    .meeting-actions {
      display: flex;
      gap: 0.5rem; /* var(--spacing-sm) */
      margin-top: 1.5rem;
      justify-content: flex-end;
    }

    .loading {
      text-align: center;
      padding: 2rem; /* var(--spacing-lg) */
      color: var(--text-color-muted, #666);
    }

    /* Responsive adjustment for the grid */
    @media (max-width: 768px) {
      .meetings-grid {
        grid-template-columns: 1fr;
      }
    }

    /*
     * All styles for .btn and its variants have been removed
     * as they are now provided by the global styles.scss.
     */
  `]
})
export class MeetingsListComponent {
  private meetingsService = inject(MeetingsService);
  private router = inject(Router);
  // NEW: Inject the pipe to use its logic in the component
  private meetingStatusPipe = inject(MeetingStatusPipe);

  private refresh$ = new BehaviorSubject<void>(undefined);

  // NEW: Define the order of categories for the template
  readonly meetingCategories: MeetingCategory[] = ['inProgress', 'upcoming', 'finished', 'canceled'];

  // FIX: The data stream now emits the categorized object
  categorizedMeetings$: Observable<CategorizedMeetings> = this.refresh$.pipe(
    switchMap(() => this.meetingsService.getMeetings()),
    map(meetings => {
      // First, sort all meetings by start date, ascending
      const sortedMeetings = [...meetings].sort((a, b) =>
        new Date(a.startAt).getTime() - new Date(b.startAt).getTime()
      );

      // Then, categorize them
      const categorized: CategorizedMeetings = {
        upcoming: [],
        inProgress: [],
        finished: [],
        canceled: []
      };

      for (const meeting of sortedMeetings) {
        const status = this.meetingStatusPipe.transform(meeting);
        switch (status) {
          case 'In Progress':
            categorized.inProgress.push(meeting);
            break;
          case 'Upcoming':
            categorized.upcoming.push(meeting);
            break;
          case 'Finished':
            categorized.finished.push(meeting);
            break;
          case 'Canceled':
            categorized.canceled.push(meeting);
            break;
        }
      }
      return categorized;
    })
  );

  createMeeting() {
    this.router.navigate(['/meetings/new']);
  }

  viewMeeting(id: string) {
    this.router.navigate(['/meetings', id]);
  }

  editMeeting(id: string) {
    this.router.navigate(['/meetings', id, 'edit']);
  }

  cancelMeeting(id: string) {
    if (confirm('Are you sure you want to cancel this meeting?')) {
      this.meetingsService.cancelMeeting(id).subscribe({
        complete: () => this.refresh$.next()
      });
    }
  }
}
