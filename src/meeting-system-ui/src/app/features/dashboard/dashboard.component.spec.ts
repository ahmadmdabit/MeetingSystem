import { ComponentFixture, TestBed } from '@angular/core/testing';
import { DashboardComponent } from './dashboard.component';
import { Router } from '@angular/router';
import { AuthService } from '../../core/api/auth.service';
import { of } from 'rxjs';
import { provideZonelessChangeDetection } from '@angular/core';

class MockAuthService {
  logout() {
    return of(null);
  }
}

class MockRouter {
  navigate = jasmine.createSpy('navigate');
}

describe('DashboardComponent', () => {
  let component: DashboardComponent;
  let fixture: ComponentFixture<DashboardComponent>;
  let mockAuthService: MockAuthService;
  let mockRouter: MockRouter;

  beforeEach(async () => {
    mockAuthService = new MockAuthService();
    mockRouter = new MockRouter();
    
    await TestBed.configureTestingModule({
      imports: [DashboardComponent],
      providers: [
        provideZonelessChangeDetection(),
        { provide: AuthService, useValue: mockAuthService },
        { provide: Router, useValue: mockRouter }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(DashboardComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should navigate to meetings page when meetings card is clicked', () => {
    component.navigateTo('/meetings');
    
    expect(mockRouter.navigate).toHaveBeenCalledWith(['/meetings']);
  });

  it('should navigate to profile page when profile card is clicked', () => {
    component.navigateTo('/profile');
    
    expect(mockRouter.navigate).toHaveBeenCalledWith(['/profile']);
  });

  it('should navigate to users page when users card is clicked', () => {
    component.navigateTo('/users');
    
    expect(mockRouter.navigate).toHaveBeenCalledWith(['/users']);
  });
});