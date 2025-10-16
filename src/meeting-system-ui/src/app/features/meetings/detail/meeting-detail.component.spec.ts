import { API_CONFIG, DEFAULT_API_CONFIG } from '../../../core/config/api.config';
import { HttpClientTestingModule } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MeetingDetailComponent } from './meeting-detail.component';
import { Router, ActivatedRoute, convertToParamMap } from '@angular/router';
import { MeetingsService } from '../../../core/api/meetings.service';
import { firstValueFrom, Observable, of } from 'rxjs';
import { provideZonelessChangeDetection } from '@angular/core';
import { Meeting } from '../../../core/models/meeting.model';

// Mocks
const MOCK_MEETING: Meeting = {
  id: 'meeting-123',
  name: 'Test Meeting',
  description: 'Test description',
  startAt: '2025-10-15T09:00:00Z',
  endAt: '2025-10-15T10:00:00Z',
  organizerId: 'user-1',
  isCanceled: false,
  participants: []
};

// FIX: Properly type the mock service to match the real service
class MockMeetingsService {
  getMeetingById(id: string): Observable<Meeting> {
    return of(MOCK_MEETING);
  }
  cancelMeeting(id: string): Observable<void> {
    return of(undefined);
  }
}

class MockRouter {
  navigate = jasmine.createSpy('navigate');
}

describe('MeetingDetailComponent', () => {
  let component: MeetingDetailComponent;
  let fixture: ComponentFixture<MeetingDetailComponent>;
  let mockMeetingsService: MockMeetingsService;
  let mockRouter: MockRouter;

  beforeEach(async () => {
    mockMeetingsService = new MockMeetingsService();
    mockRouter = new MockRouter();

    await TestBed.configureTestingModule({
      imports: [MeetingDetailComponent, HttpClientTestingModule],
      providers: [
        provideZonelessChangeDetection(),
        { provide: MeetingsService, useValue: mockMeetingsService },
        { provide: Router, useValue: mockRouter },
        { provide: API_CONFIG, useValue: DEFAULT_API_CONFIG },
        {
          provide: ActivatedRoute,
          // FIX: Mock the paramMap as an observable to test the reactive stream
          useValue: { paramMap: of(convertToParamMap({ id: 'meeting-123' })) }
        }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(MeetingDetailComponent);
    component = fixture.componentInstance;
  });

  it('should create', () => {
    fixture.detectChanges();
    expect(component).toBeTruthy();
  });

  it('should load meeting details based on route parameter', async () => {
    const getByIdSpy = spyOn(mockMeetingsService, 'getMeetingById').and.callThrough();

    fixture.detectChanges();
    await fixture.whenStable();

    const meeting = await firstValueFrom(component.meeting$);

    expect(getByIdSpy).toHaveBeenCalledWith('meeting-123');
    expect(meeting).toEqual(MOCK_MEETING);
  });

  describe('User Actions', () => {
    beforeEach(() => {
      fixture.detectChanges();
    });

    it('should navigate back to the meetings list', () => {
      component.goBack();
      expect(mockRouter.navigate).toHaveBeenCalledWith(['/meetings']);
    });

    it('should navigate to the edit meeting page', () => {
      component.editMeeting('meeting-123');
      expect(mockRouter.navigate).toHaveBeenCalledWith(['/meetings', 'meeting-123', 'edit']);
    });

    it('should call cancelMeeting and navigate to the meetings list on confirmation', () => {
      spyOn(window, 'confirm').and.returnValue(true);
      const deleteSpy = spyOn(mockMeetingsService, 'cancelMeeting').and.returnValue(of(undefined));

      component.cancelMeeting('meeting-123');

      expect(deleteSpy).toHaveBeenCalledWith('meeting-123');
      expect(mockRouter.navigate).toHaveBeenCalledWith(['/meetings']);
    });

    it('should not call cancelMeeting if the user cancels confirmation', () => {
      spyOn(window, 'confirm').and.returnValue(false);
      const deleteSpy = spyOn(mockMeetingsService, 'cancelMeeting');

      component.cancelMeeting('meeting-123');

      expect(deleteSpy).not.toHaveBeenCalled();
    });
  });
});
