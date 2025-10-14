import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { provideZonelessChangeDetection } from '@angular/core';

import { MeetingsService } from './meetings.service';
import { API_CONFIG } from '../config/api.config';
import { Meeting, CreateMeeting, UpdateMeeting, AddParticipant } from '../models/meeting.model';

describe('MeetingsService', () => {
  let service: MeetingsService;
  let httpController: HttpTestingController;
  const baseUrl = `/api/meetings`;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideZonelessChangeDetection(),
        MeetingsService,
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: API_CONFIG, useValue: { baseUrl: '/api' } }
      ]
    });

    service = TestBed.inject(MeetingsService);
    httpController = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpController.verify());

  describe('getMeetings', () => {
    it('should retrieve all meetings', () => {
      const mockMeetings: Meeting[] = [
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
          name: 'Sprint Planning',
          description: 'Sprint 10 planning',
          startAt: '2025-10-15T14:00:00Z',
          endAt: '2025-10-15T16:00:00Z',
          organizerId: 'user-2',
          isCanceled: false,
          participants: []
        }
      ];

      service.getMeetings().subscribe(meetings => {
        expect(meetings).toEqual(mockMeetings);
        expect(meetings.length).toBe(2);
      });

      const req = httpController.expectOne(baseUrl);
      expect(req.request.method).toBe('GET');
      req.flush(mockMeetings);
    });

    it('should return empty array when no meetings exist', () => {
      service.getMeetings().subscribe(meetings => {
        expect(meetings).toEqual([]);
      });

      const req = httpController.expectOne(baseUrl);
      req.flush([]);
    });
  });

  describe('createMeeting', () => {
    it('should create a new meeting', () => {
      const newMeeting: CreateMeeting = {
        name: 'New Meeting',
        description: 'Test meeting',
        startAt: '2025-10-20T10:00:00Z',
        endAt: '2025-10-20T11:00:00Z',
        participantEmails: ['user1@example.com', 'user2@example.com']
      };

      const mockResponse: Meeting = {
        id: 'new-id',
        ...newMeeting,
        organizerId: 'current-user',
        isCanceled: false,
        participants: []
      };

      service.createMeeting(newMeeting).subscribe(meeting => {
        expect(meeting).toEqual(mockResponse);
        expect(meeting.id).toBe('new-id');
      });

      const req = httpController.expectOne(baseUrl);
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual(newMeeting);
      req.flush(mockResponse);
    });
  });

  describe('getMeetingById', () => {
    it('should retrieve a specific meeting', () => {
      const meetingId = 'meeting-123';
      const mockMeeting: Meeting = {
        id: meetingId,
        name: 'Specific Meeting',
        description: 'Details',
        startAt: '2025-10-15T09:00:00Z',
        endAt: '2025-10-15T10:00:00Z',
        organizerId: 'user-1',
        isCanceled: false
      };

      service.getMeetingById(meetingId).subscribe(meeting => {
        expect(meeting).toEqual(mockMeeting);
      });

      const req = httpController.expectOne(`${baseUrl}/${meetingId}`);
      expect(req.request.method).toBe('GET');
      req.flush(mockMeeting);
    });

    it('should handle meeting not found error', () => {
      const meetingId = 'non-existent';

      service.getMeetingById(meetingId).subscribe({
        next: () => fail('should have failed'),
        error: (error) => {
          expect(error.status).toBe(404);
        }
      });

      const req = httpController.expectOne(`${baseUrl}/${meetingId}`);
      req.flush({ message: 'Meeting not found' }, { status: 404, statusText: 'Not Found' });
    });
  });

  describe('updateMeeting', () => {
    it('should update an existing meeting', () => {
      const meetingId = 'meeting-123';
      const updateData: UpdateMeeting = {
        name: 'Updated Meeting',
        description: 'Updated description',
        startAt: '2025-10-20T14:00:00Z',
        endAt: '2025-10-20T15:00:00Z'
      };

      const mockResponse: Meeting = {
        id: meetingId,
        ...updateData,
        organizerId: 'user-1',
        isCanceled: false
      };

      service.updateMeeting(meetingId, updateData).subscribe(meeting => {
        expect(meeting).toEqual(mockResponse);
      });

      const req = httpController.expectOne(`${baseUrl}/${meetingId}`);
      expect(req.request.method).toBe('PUT');
      expect(req.request.body).toEqual(updateData);
      req.flush(mockResponse);
    });
  });

  describe('deleteMeeting', () => {
    it('should delete a meeting', () => {
      const meetingId = 'meeting-123';

      // FIX: An Observable<void> doesn't emit a value.
      // The success is implied by the 'next' callback being hit without error.
      service.deleteMeeting(meetingId).subscribe(response => {
        expect(response).toBeNull();
      });

      const req = httpController.expectOne(`${baseUrl}/${meetingId}`);
      expect(req.request.method).toBe('DELETE');
      req.flush(null); // This completes the observable.
    });
    
    // Add test for delete meeting with 403 Forbidden response
    it('should handle meeting deletion failure with forbidden', () => {
      const meetingId = 'meeting-123';

      service.deleteMeeting(meetingId).subscribe({
        error: (error) => {
          expect(error.status).toBe(403);
        }
      });

      const req = httpController.expectOne(`${baseUrl}/${meetingId}`);
      req.flush({ message: 'Forbidden' }, { status: 403, statusText: 'Forbidden' });
    });
  });

  describe('addParticipant', () => {
    it('should add a participant to a meeting', () => {
      const meetingId = 'meeting-123';
      const participant: AddParticipant = {
        email: 'newuser@example.com'
      };

      // FIX: Corrected expectation for Observable<void>
      service.addParticipant(meetingId, participant).subscribe(response => {
        expect(response).toBeNull();
      });

      const req = httpController.expectOne(`${baseUrl}/${meetingId}/participants`);
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual(participant);
      req.flush(null);
    });
  });

  describe('removeParticipant', () => {
    it('should remove a participant from a meeting', () => {
      const meetingId = 'meeting-123';
      const userId = 'user-456';

      // FIX: Corrected expectation for Observable<void>
      service.removeParticipant(meetingId, userId).subscribe(response => {
        expect(response).toBeNull();
      });

      const req = httpController.expectOne(`${baseUrl}/${meetingId}/participants/${userId}`);
      expect(req.request.method).toBe('DELETE');
      req.flush(null);
    });
  });
});
