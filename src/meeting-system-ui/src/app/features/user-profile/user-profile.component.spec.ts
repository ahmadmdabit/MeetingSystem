import { ComponentFixture, TestBed } from '@angular/core/testing';
import { UserProfileComponent } from './user-profile.component';
import { ReactiveFormsModule } from '@angular/forms';
import { UserProfileService } from '../../core/api/user-profile.service';
import { firstValueFrom, of, throwError } from 'rxjs';
import { provideZonelessChangeDetection } from '@angular/core';
import { UserProfile, UpdateUserProfile } from '../../core/models/user.model';
import { ChangeDetectorRef } from '@angular/core';

// A complete mock user profile that matches the UserProfile model
const MOCK_USER_PROFILE: UserProfile = {
  id: 'user-123',
  firstName: 'John',
  lastName: 'Doe',
  email: 'john.doe@example.com',
  phone: '+1234567890',
  profilePictureUrl: 'http://example.com/pic.jpg'
};

// A mock service that correctly implements the service's methods
class MockUserProfileService {
  getMe() {
    return of(MOCK_USER_PROFILE);
  }

  updateMe(user: UpdateUserProfile) {
    return of(undefined); // Methods returning Observable<void> should emit undefined
  }

  uploadProfilePicture(file: File) {
    return of(undefined);
  }

  deleteProfilePicture() {
    return of(undefined);
  }
}

describe('UserProfileComponent', () => {
  let component: UserProfileComponent;
  let fixture: ComponentFixture<UserProfileComponent>;
  let mockUserProfileService: MockUserProfileService;

  beforeEach(async () => {
    mockUserProfileService = new MockUserProfileService();

    await TestBed.configureTestingModule({
      imports: [UserProfileComponent, ReactiveFormsModule],
      providers: [
        provideZonelessChangeDetection(),
        { provide: UserProfileService, useValue: mockUserProfileService },
        // Provide a mock ChangeDetectorRef
        { provide: ChangeDetectorRef, useValue: { markForCheck: () => {} } }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(UserProfileComponent);
    component = fixture.componentInstance;
    // Note: We call detectChanges() inside each test for better control
  });

  it('should create', () => {
    fixture.detectChanges();
    expect(component).toBeTruthy();
  });

  it('should load user profile and patch the form on initialization', async () => {
    spyOn(mockUserProfileService, 'getMe').and.returnValue(of(MOCK_USER_PROFILE));

    fixture.detectChanges();
    await fixture.whenStable();

    const profile = await firstValueFrom(component.userProfile$);
    expect(profile).toEqual(MOCK_USER_PROFILE);

    expect(component.profileForm.value).toEqual({
      firstName: 'John',
      lastName: 'Doe',
      phone: '+1234567890'
    });
  });

  it('should have a valid form when all required fields are filled', () => {
    fixture.detectChanges();
    component.profileForm.controls['firstName'].setValue('John');
    component.profileForm.controls['lastName'].setValue('Doe');
    // Email is disabled but should have a value for the form to be valid
    component.profileForm.controls['email'].setValue('john.doe@example.com');

    expect(component.profileForm.valid).toBe(true);
  });

  it('should have an invalid form when a required field is empty', () => {
    fixture.detectChanges();
    component.profileForm.controls['firstName'].setValue('');
    component.profileForm.controls['lastName'].setValue('Doe');

    expect(component.profileForm.invalid).toBe(true);
  });

  it('should call updateMe with the correct payload on submit', () => {
    fixture.detectChanges();
    const updateSpy = spyOn(mockUserProfileService, 'updateMe').and.returnValue(of(undefined));

    component.profileForm.patchValue({
      firstName: 'Jane',
      lastName: 'Smith',
      phone: '+9876543210'
    });

    component.onSubmit();

    expect(updateSpy).toHaveBeenCalledWith({
      firstName: 'Jane',
      lastName: 'Smith',
      phone: '+9876543210'
    });
    expect(component.isLoading).toBe(false);
  });

  it('should set the error message on profile update failure', () => {
    fixture.detectChanges();
    const errorResponse = { error: { message: 'Update failed' } };
    spyOn(mockUserProfileService, 'updateMe').and.returnValue(throwError(() => errorResponse));

    component.profileForm.patchValue({ firstName: 'Jane', lastName: 'Smith' });
    component.onSubmit();

    expect(component.error).toBe('Update failed');
    expect(component.isLoading).toBe(false);
  });

  it('should call uploadPicture when a file is selected', () => {
    fixture.detectChanges();
    const mockFile = new File(['image'], 'profile.jpg', { type: 'image/jpeg' });
    const event = { target: { files: [mockFile] } };
    const uploadSpy = spyOn(component, 'uploadPicture');

    component.onFileSelected(event as any);

    expect(uploadSpy).toHaveBeenCalledWith(mockFile);
  });

  it('should call the deleteProfilePicture service and reload the profile', () => {
    fixture.detectChanges();
    const deleteSpy = spyOn(mockUserProfileService, 'deleteProfilePicture').and.returnValue(of(undefined));
    const loadSpy = spyOn(component, 'loadUserProfile');

    component.deletePicture();

    expect(deleteSpy).toHaveBeenCalled();
    expect(loadSpy).toHaveBeenCalled();
  });
});
