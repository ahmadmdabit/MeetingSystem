import { API_CONFIG, DEFAULT_API_CONFIG } from '../../../core/config/api.config';
import { HttpClientTestingModule } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { UsersListComponent } from './users-list.component';
import { UsersService } from '../../../core/api/users.service';
import { of, Observable } from 'rxjs';
import { provideZonelessChangeDetection } from '@angular/core';
import { UserProfile } from '../../../core/models/user.model';

class MockUsersService {
  getUsers(): Observable<UserProfile[]> {
    return of([]); // Default empty array
  }
}

describe('UsersListComponent', () => {
  let component: UsersListComponent;
  let fixture: ComponentFixture<UsersListComponent>;
  let mockUsersService: MockUsersService;

  beforeEach(async () => {
    mockUsersService = new MockUsersService();

    await TestBed.configureTestingModule({
      imports: [UsersListComponent, HttpClientTestingModule],
      providers: [
        provideZonelessChangeDetection(),
        { provide: UsersService, useValue: mockUsersService },
        { provide: API_CONFIG, useValue: DEFAULT_API_CONFIG }
      ]
    }).compileComponents();
  });

  it('should create', () => {
    fixture = TestBed.createComponent(UsersListComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
    expect(component).toBeTruthy();
  });

  it('should load and display users when data is returned', async () => {
    const mockUsers: UserProfile[] = [
      { id: 'user-1', firstName: 'John', lastName: 'Doe', email: 'john.doe@example.com', phone: '+1234567890', profilePictureUrl: '' },
      { id: 'user-2', firstName: 'Jane', lastName: 'Smith', email: 'jane.smith@example.com', phone: '+9876543210', profilePictureUrl: '' }
    ];

    spyOn(mockUsersService, 'getUsers').and.returnValue(of(mockUsers));

    fixture = TestBed.createComponent(UsersListComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
    await fixture.whenStable();

    const userCards = fixture.nativeElement.querySelectorAll('.user-card');
    const firstCardName = userCards[0].querySelector('h4');
    const loadingEl = fixture.nativeElement.querySelector('.loading');

    expect(userCards.length).toBe(2);
    expect(firstCardName.textContent).toContain('John Doe');
    // The loading element should not be present after data has loaded
    expect(loadingEl).toBeFalsy();
  });

  it('should display an empty state when no users are returned', async () => {
    // The default mock already returns an empty array, so no spy is needed.

    fixture = TestBed.createComponent(UsersListComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
    await fixture.whenStable();

    const userCards = fixture.nativeElement.querySelectorAll('.user-card');
    const loadingEl = fixture.nativeElement.querySelector('.loading');

    // Assert that no user cards are rendered
    expect(userCards.length).toBe(0);
    // The loading element should also be gone, and the @if block should just be empty.
    expect(loadingEl).toBeFalsy();
  });
});
