import { AuthService } from '../../../core/api/auth.service';
import { API_CONFIG, DEFAULT_API_CONFIG } from '../../../core/config/api.config';
import { HttpClientTestingModule } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MeetingFormComponent } from './meeting-form.component';
import { ReactiveFormsModule } from '@angular/forms';
import { Router, ActivatedRoute, convertToParamMap } from '@angular/router';
import { MeetingsService } from '../../../core/api/meetings.service';
import { of, throwError } from 'rxjs';
import { provideZonelessChangeDetection } from '@angular/core';
import { Meeting, CreateMeeting, UpdateMeeting } from '../../../core/models/meeting.model';

// Mocks
const MOCK_MEETING: Meeting = {
  id: 'meeting-123',
  name: 'Test Meeting',
  description: 'Test description',
  startAt: '2025-10-15T09:00:00Z',
  endAt: '2025-10-15T10:00:00Z',
  organizerId: 'user-1',
  isCanceled: false,
  participants: [{ userId: 'p1', email: 'p1@test.com' }]
};

class MockMeetingsService {
  getMeetingById(id: string) {
    return of(MOCK_MEETING);
  }
  createMeeting(meeting: CreateMeeting) {
    return of({ ...MOCK_MEETING, id: 'new-id', ...meeting });
  }
  updateMeeting(id: string, meeting: UpdateMeeting) {
    return of({ ...MOCK_MEETING, ...meeting });
  }
}

class MockRouter {
  navigate = jasmine.createSpy('navigate');
}

class MockAuthService {
  getCurrentUser() {
    return { id: 'user-1' };
  }
  isAdmin() {
    return false;
  }
}

describe('MeetingFormComponent', () => {
  let component: MeetingFormComponent;
  let fixture: ComponentFixture<MeetingFormComponent>;
  let mockMeetingsService: MockMeetingsService;
  let mockRouter: MockRouter;
  let mockAuthService: MockAuthService;

  // Helper function to set up the TestBed with a specific route
  const setup = async (routeParams: { [key: string]: string } = {}) => {
    mockMeetingsService = new MockMeetingsService();
    mockRouter = new MockRouter();
    mockAuthService = new MockAuthService();

    await TestBed.configureTestingModule({
      imports: [MeetingFormComponent, ReactiveFormsModule, HttpClientTestingModule],
      providers: [
        provideZonelessChangeDetection(),
        { provide: MeetingsService, useValue: mockMeetingsService },
        { provide: Router, useValue: mockRouter },
        { provide: AuthService, useValue: mockAuthService },
        { provide: API_CONFIG, useValue: DEFAULT_API_CONFIG },
        {
          provide: ActivatedRoute,
          useValue: { paramMap: of(convertToParamMap(routeParams)) }
        }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(MeetingFormComponent);
    component = fixture.componentInstance;
  };

  describe('in Create Mode', () => {
    beforeEach(async () => {
      await setup(); // No route params
      fixture.detectChanges();
    });

    it('should initialize in create mode', () => {
      expect(fixture.nativeElement.querySelector('h2').textContent).toBe('Create Meeting');
    });

    it('should call createMeeting on submit', () => {
      const createSpy = spyOn(mockMeetingsService, 'createMeeting').and.callThrough();
      component.meetingForm.setValue({
        name: 'New Meeting',
        description: 'A new one',
        startAt: '2025-10-20T10:00',
        endAt: '2025-10-20T11:00',
        participantEmails: 'p1@test.com, p2@test.com'
      });

      component.onSubmit({ isEditing: false });

      expect(createSpy).toHaveBeenCalled();
      expect(mockRouter.navigate).toHaveBeenCalledWith(['/meetings', 'new-id', 'edit']);
    });

    it('should navigate to meetings list on cancel', () => {
      component.cancel({ isEditing: false });
      expect(mockRouter.navigate).toHaveBeenCalledWith(['/meetings']);
    });
  });

  describe('in Edit Mode', () => {
    beforeEach(async () => {
      await setup({ id: 'meeting-123' });
    });

    it('should initialize in edit mode and load data', async () => {
      const loadSpy = spyOn(mockMeetingsService, 'getMeetingById').and.callThrough();
      fixture.detectChanges(); // Triggers ngOnInit
      await fixture.whenStable(); // Wait for observables

      expect(fixture.nativeElement.querySelector('h2').textContent).toBe('Edit Meeting');
      expect(loadSpy).toHaveBeenCalledWith('meeting-123');
      expect(component.meetingForm.value.name).toBe('Test Meeting');
    });

    it('should call updateMeeting on submit', () => {
      fixture.detectChanges();
      const updateSpy = spyOn(mockMeetingsService, 'updateMeeting').and.callThrough();
      component.meetingForm.patchValue({ name: 'Updated Name' });

      component.onSubmit({ isEditing: true, meeting: MOCK_MEETING });

      expect(updateSpy).toHaveBeenCalled();
      // No navigation on update, so the following line is removed
      // expect(mockRouter.navigate).toHaveBeenCalledWith(['/meetings', 'meeting-123']);
    });

    it('should navigate back to meeting details on cancel', () => {
      fixture.detectChanges();
      component.cancel({ isEditing: true, meeting: MOCK_MEETING });
      expect(mockRouter.navigate).toHaveBeenCalledWith(['/meetings', 'meeting-123']);
    });
  });

  describe('Form Validation', () => {
    beforeEach(async () => {
      await setup();
      fixture.detectChanges();
    });

    it('should be invalid when required fields are empty', () => {
      expect(component.meetingForm.valid).toBe(false);
    });

    it('should be valid when required fields are filled correctly', () => {
      component.meetingForm.patchValue({
        name: 'Valid Meeting',
        startAt: '2025-10-20T10:00',
        endAt: '2025-10-20T11:00'
      });
      expect(component.meetingForm.valid).toBe(true);
    });

    it('should be invalid if end time is not after start time', () => {
      component.meetingForm.patchValue({
        name: 'Valid Meeting',
        startAt: '2025-10-20T11:00',
        endAt: '2025-10-20T10:00'
      });
      expect(component.meetingForm.valid).toBe(false);
      expect(component.meetingForm.hasError('endTimeAfterStart')).toBe(true);
    });
  });
});
