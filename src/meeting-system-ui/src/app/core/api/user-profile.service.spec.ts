import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { provideZonelessChangeDetection } from '@angular/core';

import { UserProfileService } from './user-profile.service';
import { API_CONFIG } from '../config/api.config';
import { UpdateUserProfile, UserProfile } from '../models/user.model';

describe('UserProfileService', () => {
  let service: UserProfileService;
  let httpController: HttpTestingController;
  const baseUrl = `/api/users/me`;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideZonelessChangeDetection(),
        UserProfileService,
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: API_CONFIG, useValue: { baseUrl: '/api' } }
      ]
    });

    service = TestBed.inject(UserProfileService);
    httpController = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpController.verify());

  const mockProfile: UserProfile = {
      id: 'user-123',
      firstName: 'John',
      lastName: 'Doe',
      email: 'john.doe@example.com',
      phone: '+1234567890',
      profilePictureUrl: 'http://example.com/pic.jpg'
    };

  describe('getMe', () => {
    it('should retrieve current user profile', () => {
      service.getMe().subscribe(profile => {
        expect(profile).toEqual(mockProfile);
        expect(profile.email).toBe('john.doe@example.com');
      });

      const req = httpController.expectOne(baseUrl);
      expect(req.request.method).toBe('GET');
      req.flush(mockProfile);
    });

    it('should handle unauthorized access', () => {
      service.getMe().subscribe({
        next: () => fail('should have failed'),
        error: (error) => {
          expect(error.status).toBe(401);
        }
      });

      const req = httpController.expectOne(baseUrl);
      req.flush({ message: 'Unauthorized' }, { status: 401, statusText: 'Unauthorized' });
    });
  });

  describe('updateMe', () => {
    it('should update user profile', () => {
      const updateData: UpdateUserProfile = {
        firstName: 'Jane',
        lastName: 'Smith',
        phone: '+9876543210'
      };

      // FIX: Corrected expectation for Observable<void>
      service.updateMe(updateData).subscribe();

      const req = httpController.expectOne(baseUrl);
      expect(req.request.method).toBe('PUT');
      expect(req.request.body).toEqual(updateData);
      req.flush(null);
    });

    it('should handle partial profile update', () => {
      const updateData: UpdateUserProfile = {
        phone: '+1111111111'
      };

      service.updateMe(updateData).subscribe();

      const req = httpController.expectOne(baseUrl);
      expect(req.request.body).toEqual(updateData);
      req.flush(null);
    });
  });

  describe('uploadProfilePicture', () => {
    it('should upload profile picture', () => {
      const mockFile = new File(['image-data'], 'profile.jpg', { type: 'image/jpeg' });

      // FIX: Corrected expectation for Observable<void>
      service.uploadProfilePicture(mockFile).subscribe();

      const req = httpController.expectOne(`${baseUrl}/profile-picture`);
      expect(req.request.method).toBe('PUT');
      expect(req.request.body instanceof FormData).toBeTruthy();
      req.flush(null);
    });

    it('should handle invalid file type error', () => {
      const mockFile = new File(['text'], 'document.txt', { type: 'text/plain' });

      service.uploadProfilePicture(mockFile).subscribe({
        next: () => fail('should have failed'),
        error: (error) => {
          expect(error.status).toBe(400);
        }
      });

      const req = httpController.expectOne(`${baseUrl}/profile-picture`);
      req.flush({ message: 'Invalid file type' }, { status: 400, statusText: 'Bad Request' });
    });
  });

  describe('deleteProfilePicture', () => {
    it('should delete profile picture', () => {
      // FIX: Corrected expectation for Observable<void>
      service.deleteProfilePicture().subscribe();

      const req = httpController.expectOne(`${baseUrl}/profile-picture`);
      expect(req.request.method).toBe('DELETE');
      req.flush(null);
    });

    it('should handle delete when no picture exists', () => {
      service.deleteProfilePicture().subscribe({
        next: () => fail('should have failed'),
        error: (error) => {
          expect(error.status).toBe(404);
        }
      });

      const req = httpController.expectOne(`${baseUrl}/profile-picture`);
      req.flush({ message: 'No profile picture found' }, { status: 404, statusText: 'Not Found' });
    });
  });
});
