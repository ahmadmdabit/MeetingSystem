import { Component, inject } from '@angular/core';
import { Router } from '@angular/router';
import { AuthService } from '../../core/api/auth.service';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  template: `
    <!-- The header is now part of the MainLayoutComponent -->
    <div class="dashboard-main">
      <div class="welcome-section">
        <h2>Welcome to the Meeting System!</h2>
        <p>Manage your meetings, participants, and files in one place.</p>
      </div>

      <div class="quick-actions">
        <h3>Quick Actions</h3>
        <div class="actions-grid">
          <div class="action-card" (click)="navigateTo('/meetings')">
            <h4>Manage Meetings</h4>
            <p>View, create, and update your meetings</p>
          </div>
          <div class="action-card" (click)="navigateTo('/profile')">
            <h4>Profile Settings</h4>
            <p>Update your personal information</p>
          </div>
          <div class="action-card" (click)="navigateTo('/users')">
            <h4>Manage Users</h4>
            <p>View and manage system users</p>
          </div>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .welcome-section {
      text-align: center;
      margin-bottom: 3rem;
      background: white;
      padding: 2rem;
      border-radius: 8px;
      box-shadow: 0 2px 8px rgba(0, 0, 0, 0.1);
    }

    .welcome-section h2 {
      color: #333; /* Or use a CSS variable like var(--text-color-dark) */
      margin-top: 0;
      margin-bottom: 1rem;
    }

    .welcome-section p {
      color: #666; /* Or use var(--text-color-muted) */
      font-size: 1.1rem;
      margin-bottom: 0;
    }

    .quick-actions h3 {
      margin-top: 0;
      margin-bottom: 1.5rem;
      color: #333; /* Or use var(--text-color-dark) */
    }

    .actions-grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(250px, 1fr));
      gap: 1.5rem;
    }

    .action-card {
      background: white;
      border-radius: 8px;
      padding: 1.5rem;
      box-shadow: 0 2px 8px rgba(0, 0, 0, 0.1);
      cursor: pointer;
      transition: transform 0.2s, box-shadow 0.2s;
    }

    .action-card:hover {
      transform: translateY(-4px);
      box-shadow: 0 4px 12px rgba(0, 0, 0, 0.15);
    }

    .action-card h4 {
      margin-top: 0;
      margin-bottom: 0.5rem;
      color: #333; /* Or use var(--text-color-dark) */
    }

    .action-card p {
      margin: 0;
      color: #666; /* Or use var(--text-color-muted) */
    }

    /* Responsive adjustment for the grid */
    @media (max-width: 768px) {
      .actions-grid {
        grid-template-columns: 1fr; /* Stack to a single column on smaller screens */
      }
    }
  `]
})
export class DashboardComponent {
  private router = inject(Router);
  // The AuthService is no longer needed here for logout

  // The logout method is removed from here.

  navigateTo(path: string) {
    this.router.navigate([path]);
  }
}
