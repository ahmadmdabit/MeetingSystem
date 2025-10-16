import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MeetingsListComponent } from './meetings-list.component';
import { Router } from '@angular/router';
import { MeetingsService } from '../../../core/api/meetings.service';
import { Observable, of } from 'rxjs';
import { provideZonelessChangeDetection } from '@angular/core';
import { Meeting } from '../../../core/models/meeting.model';

// A mock meeting object for consistent test data
const MOCK_MEETINGS: Meeting[] = [
  {
    id: '1',
    name: 'Team Standup',
    description: 'Daily standup',
    startAt: '2025-10-15T09:00:00Z',
    endAt: '2025-10-15T09:30:00Z',
    organizerId: 'user-1',
    isCanceled: false,
    participants: []
  },
  {
    id: '2',
    name: 'Sprint Review',
    description: 'Review of the sprint',
    startAt: '2025-10-16T14:00:00Z',
    endAt: '2025-10-16T15:00:00Z',
    organizerId: 'user-2',
    isCanceled: true,
    participants: []
  }
];

class MockMeetingsService {
  getMeetings(): Observable<Meeting[]> {
    return of(MOCK_MEETINGS);
  }
  cancelMeeting(id: string): Observable<void> {
    return of(undefined);
  }
}

class MockRouter {
  navigate = jasmine.createSpy('navigate');
}

describe('MeetingsListComponent', () => {
  let component: MeetingsListComponent;
  let fixture: ComponentFixture<MeetingsListComponent>;
  let mockMeetingsService: MockMeetingsService;
  let mockRouter: MockRouter;

  beforeEach(async () => {
    mockMeetingsService = new MockMeetingsService();
    mockRouter = new MockRouter();

    await TestBed.configureTestingModule({
      imports: [MeetingsListComponent],
      providers: [
        provideZonelessChangeDetection(),
        { provide: MeetingsService, useValue: mockMeetingsService },
        { provide: Router, useValue: mockRouter }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(MeetingsListComponent);
    component = fixture.componentInstance;
  });

  it('should create', () => {
    fixture.detectChanges();
    expect(component).toBeTruthy();
  });

  it('should load and display meetings on initialization', async () => {
    const getMeetingsSpy = spyOn(mockMeetingsService, 'getMeetings').and.callThrough();

    fixture.detectChanges(); // Triggers the declarative stream and async pipe
    await fixture.whenStable(); // Wait for async operations to complete

    // FIX: Assert against the rendered DOM, not by re-subscribing to the observable.
    const cards = fixture.nativeElement.querySelectorAll('.meeting-card');
    const firstCardTitle = cards[0].querySelector('h3');

    // The service should only be called once.
    expect(getMeetingsSpy).toHaveBeenCalledTimes(1);
    // The number of rendered cards should match the mock data.
    expect(cards.length).toBe(MOCK_MEETINGS.length);
    // The content of the cards should be correct.
    expect(firstCardTitle.textContent).toContain('Team Standup');
  });

  describe('Navigation', () => {
    beforeEach(() => fixture.detectChanges());

    it('should navigate to the create meeting page', () => {
      component.createMeeting();
      expect(mockRouter.navigate).toHaveBeenCalledWith(['/meetings/new']);
    });

    it('should navigate to the view meeting page', () => {
      component.viewMeeting('meeting-123');
      expect(mockRouter.navigate).toHaveBeenCalledWith(['/meetings', 'meeting-123']);
    });

    it('should navigate to the edit meeting page', () => {
      component.editMeeting('meeting-123');
      expect(mockRouter.navigate).toHaveBeenCalledWith(['/meetings', 'meeting-123', 'edit']);
    });
  });

  describe('cancelMeeting', () => {
    beforeEach(() => {
      spyOn(window, 'confirm').and.returnValue(true);
    });

    it('should call the cancelMeeting service and trigger a refresh', async () => {
      const getMeetingsSpy = spyOn(mockMeetingsService, 'getMeetings').and.callThrough();
      const cancelMeetingSpy = spyOn(mockMeetingsService, 'cancelMeeting').and.returnValue(of(undefined));

      fixture.detectChanges();
      await fixture.whenStable();
      // The initial call from component initialization
      expect(getMeetingsSpy).toHaveBeenCalledTimes(1);

      // Action: Delete a meeting
      component.cancelMeeting('meeting-123');
      await fixture.whenStable();

      // Assertions
      expect(cancelMeetingSpy).toHaveBeenCalledWith('meeting-123');
      // The refresh mechanism should have caused getMeetings to be called a second time
      expect(getMeetingsSpy).toHaveBeenCalledTimes(2);
    });

    it('should not call cancelMeeting if the user cancels confirmation', () => {
      (window.confirm as jasmine.Spy).and.returnValue(false);
      const cancelMeetingSpy = spyOn(mockMeetingsService, 'cancelMeeting');

      fixture.detectChanges();
      component.cancelMeeting('meeting-123');

      expect(cancelMeetingSpy).not.toHaveBeenCalled();
    });
  });
});
