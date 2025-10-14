import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { provideZonelessChangeDetection } from '@angular/core';

import { UsersService } from './users.service';
import { API_CONFIG } from '../config/api.config';
import { UserProfile, AssignRole } from '../models/user.model';
import { PresignedUrl } from '../models/file.model';

describe('UsersService', () => {
  let service: UsersService;
  let httpController: HttpTestingController;
  const baseUrl = `/api/users`;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideZonelessChangeDetection(),
        UsersService,
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: API_CONFIG, useValue: { baseUrl: '/api' } }
      ]
    });

    service = TestBed.inject(UsersService);
    httpController = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpController.verify());

  describe('getUsers', () => {
    it('should retrieve all users', () => {
      const mockUsers: UserProfile[] = [
        {
          id: 'user-1',
          firstName: 'John',
          lastName: 'Doe',
          email: 'john@example.com',
          phone: '+1234567890'
        },
        {
          id: 'user-2',
          firstName: 'Jane',
          lastName: 'Smith',
          email: 'jane@example.com',
          phone: '+9876543210'
        }
      ];

      service.getUsers().subscribe(users => {
        expect(users).toEqual(mockUsers);
        expect(users.length).toBe(2);
      });

      const req = httpController.expectOne(baseUrl);
      expect(req.request.method).toBe('GET');
      req.flush(mockUsers);
    });

    it('should return empty array when no users exist', () => {
      service.getUsers().subscribe(users => {
        expect(users).toEqual([]);
      });

      const req = httpController.expectOne(baseUrl);
      req.flush([]);
    });
  });

  describe('getUserProfilePictureUrl', () => {
    it('should retrieve presigned URL for user profile picture', () => {
      const userId = 'user-123';
      const mockUrl: PresignedUrl = {
        url: 'https://s3.amazonaws.com/profiles/user-123.jpg?signature=abc'
      };

      service.getUserProfilePictureUrl(userId).subscribe(presignedUrl => {
        expect(presignedUrl).toEqual(mockUrl);
        expect(presignedUrl.url).toContain('s3.amazonaws.com');
      });

      const req = httpController.expectOne(`${baseUrl}/${userId}/profile-picture`);
      expect(req.request.method).toBe('GET');
      req.flush(mockUrl);
    });

    it('should handle missing profile picture', () => {
      const userId = 'user-123';

      service.getUserProfilePictureUrl(userId).subscribe({
        next: () => fail('should have failed'),
        error: (error) => {
          expect(error.status).toBe(404);
        }
      });

      const req = httpController.expectOne(`${baseUrl}/${userId}/profile-picture`);
      req.flush({ message: 'Profile picture not found' }, { status: 404, statusText: 'Not Found' });
    });
  });

  describe('assignRole', () => {
    it('should assign a role to a user', () => {
      const userId = 'user-123';
      const roleData: AssignRole = {
        roleName: 'Admin'
      };

      // FIX: Corrected expectation for Observable<void>
      service.assignRole(userId, roleData).subscribe();

      const req = httpController.expectOne(`${baseUrl}/${userId}/roles`);
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual(roleData);
      req.flush(null);
    });

    it('should handle duplicate role assignment', () => {
      const userId = 'user-123';
      const roleData: AssignRole = {
        roleName: 'Admin'
      };

      service.assignRole(userId, roleData).subscribe({
        next: () => fail('should have failed'),
        error: (error) => {
          expect(error.status).toBe(409);
        }
      });

      const req = httpController.expectOne(`${baseUrl}/${userId}/roles`);
      req.flush({ message: 'Role already assigned' }, { status: 409, statusText: 'Conflict' });
    });
  });

  describe('removeRole', () => {
    it('should remove a role from a user', () => {
      const userId = 'user-123';
      const roleName = 'Admin';

      // FIX: Corrected expectation for Observable<void>
      service.removeRole(userId, roleName).subscribe();

      const req = httpController.expectOne(`${baseUrl}/${userId}/roles/${roleName}`);
      expect(req.request.method).toBe('DELETE');
      req.flush(null);
    });

    it('should handle removing non-existent role', () => {
      const userId = 'user-123';
      const roleName = 'NonExistentRole';

      service.removeRole(userId, roleName).subscribe({
        next: () => fail('should have failed'),
        error: (error) => {
          expect(error.status).toBe(404);
        }
      });

      const req = httpController.expectOne(`${baseUrl}/${userId}/roles/${roleName}`);
      req.flush({ message: 'Role not found' }, { status: 404, statusText: 'Not Found' });
    });
  });
});
