import { Component, inject } from '@angular/core';
import { UsersService } from '../../../core/api/users.service';
import { UserProfile } from '../../../core/models/user.model';
import { AsyncPipe } from '@angular/common';
import { Observable, of, forkJoin } from 'rxjs';
import { map, startWith, catchError } from 'rxjs/operators';
import { ViewState } from '../../../core/models/view-state.model'; // Import the new interface
import { AuthService } from '../../../core/api/auth.service';

@Component({
  selector: 'app-users-list',
  standalone: true,
  imports: [AsyncPipe],
  template: `
    <div class="users-list-container">
      <div class="header">
        <h2>Users</h2>
      </div>

      <!-- Bind to the new state object -->
      @if (usersState$ | async; as state) {
        <!-- Show loading state -->
        @if (state.isLoading) {
          <div class="loading">Loading users ⌛</div>
        }
        <!-- Show error state -->
        @if (state.error) {
          <div class="alert alert-danger">
            <h4>Access Denied</h4>
            <p>{{ state.error }}</p>
          </div>
        }
        <!-- Show data state -->
        @if (state.data) {
          <div class="users-grid">
            @for (user of state.data; track user.id) {
              <div class="user-card">
                <div class="user-info">
                  <div class="user-avatar">👤</div>
                  <div class="user-details">
                    <h4>{{ user.firstName }} {{ user.lastName }}</h4>
                    <p class="user-email">{{ user.email }}</p>
                    @if (user.phone) {
                      <p class="user-phone">{{ user.phone }}</p>
                    }
                  </div>
                  @if (isAdmin) {
                    <div class="user-actions">
                      <div class="role-toggle">
                        <label class="switch">
                          <input type="checkbox"
                                 role="switch"
                                 [attr.aria-checked]="hasRole(user, 'Admin')"
                                 aria-label="Toggle admin role"
                                 [checked]="hasRole(user, 'Admin')"
                                 (change)="onRoleSwitch(user, $event.target.checked)" />
                          <span class="slider"></span>
                        </label>
                        <span class="toggle-label">{{ hasRole(user, 'Admin') ? 'Admin' : 'User' }}</span>
                      </div>
                    </div>
                  }
                </div>
              </div>
            }
          </div>
        }
      }
    </div>
  `,
  styles: [`
    :host {
      display: block;
    }

    .header {
      margin-bottom: 2rem; /* var(--spacing-lg) */
    }

    .header h2 {
      margin: 0;
      color: var(--text-color-dark, #333);
    }

    .users-grid {
      display: grid;
      /* This is already nicely responsive */
      grid-template-columns: repeat(auto-fit, minmax(300px, 1fr));
      gap: 1.5rem;
    }

    .user-card {
      display: flex;
      justify-content: space-between;
      align-items: center;
      background: var(--background-color-white, white);
      border-radius: 8px;
      box-shadow: var(--box-shadow, 0 2px 8px rgba(0,0,0,0.1));
      padding: 1.5rem;
    }

    .user-info {
      display: flex;
      align-items: center;
      width: 100%;
    }

    .user-avatar {
      font-size: 2rem;
      margin-right: 1rem; /* var(--spacing-base) */
      width: 50px;
      height: 50px;
      display: flex;
      align-items: center;
      justify-content: center;
      background: var(--background-color-light, #f8f9fa);
      border-radius: 50%;
    }

    .user-details h4 {
      margin: 0 0 0.25rem 0;
      color: var(--text-color-dark, #333);
    }

    .user-actions {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      margin-left: auto;
    }

    /* Toggle switch styles */
    .role-toggle {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 0.5rem;
    }
    .switch {
      position: relative;
      display: inline-block;
      width: 44px;
      height: 24px;
      vertical-align: middle;
    }
    .switch input {
      opacity: 0;
      width: 0;
      height: 0;
    }
    .slider {
      position: absolute;
      cursor: pointer;
      inset: 0;
      background-color: var(--border-color, #ccc);
      transition: 0.2s;
      border-radius: 24px;
    }
    .slider:before {
      position: absolute;
      content: "";
      height: 18px;
      width: 18px;
      left: 3px;
      bottom: 3px;
      background-color: #fff;
      border-radius: 50%;
      box-shadow: 0 1px 2px rgba(0,0,0,0.2);
      transition: 0.2s;
    }
    .switch input:checked + .slider {
      background-color: var(--primary-color, #007bff);
    }
    .switch input:focus + .slider {
      box-shadow: 0 0 0 3px rgba(0, 123, 255, 0.25);
    }
    .switch input:checked + .slider:before {
      transform: translateX(20px);
    }
    .toggle-label {
      font-size: 0.875rem;
      color: var(--text-color-dark, #333);
      user-select: none;
      min-width: 48px;
      text-align: left;
    }

    .user-email {
      margin: 0 0 0.25rem 0;
      color: var(--text-color-muted, #666);
      font-weight: 500;
    }

    .user-phone {
      margin: 0;
      color: #888; /* A slightly different muted color, can be a new variable if needed */
    }

    .loading {
      text-align: center;
      padding: 2rem; /* var(--spacing-lg) */
      color: var(--text-color-muted, #666);
    }

    .alert {
      padding: 1rem 1.25rem;
      margin-bottom: 1rem;
      border: 1px solid transparent;
      border-radius: 0.375rem;
    }
    .alert-danger {
      color: #dc3545;
      background-color: #f8d7da;
      border-color: #f5c6cb;
    }
    .alert h4 {
      margin-top: 0;
      margin-bottom: 0.5rem;
    }
    .alert p {
      margin-bottom: 0;
    }
  `]
})
export class UsersListComponent {
  private usersService = inject(UsersService);
  private authService = inject(AuthService);

  // Create a signal to check if the current user is an admin
  public readonly isAdmin = this.authService.isAdmin();

  // Refactor the stream to emit a ViewState object
  usersState$: Observable<ViewState<UserProfile[]>> = this.usersService.getUsers().pipe(
    // 1. Map the successful response to a data state
    map(users => ({ data: users, isLoading: false })),
    // 2. Catch any errors and map them to an error state
    catchError(error => {
      const errorMessage = error.status === 403
        ? "You do not have permission to view this page."
        : "Failed to load users. Please try again later.";
      return of({ error: errorMessage, isLoading: false });
    }),
    // 3. Emit an initial loading state immediately
    startWith({ isLoading: true })
  );

  assignRole(userId: string, roleName: 'Admin' | 'User'): void {
    if (!confirm(`Are you sure you want to assign the role "${roleName}" to this user?`)) {
      return;
    }
    this.usersService.assignRole(userId, { roleName }).subscribe({
      // Optionally, you can add a success notification and refresh the list
      error: (err) => console.error('Failed to assign role', err)
    });
  }

  removeRole(userId: string, roleName: 'Admin' | 'User'): void {
    if (!confirm(`Are you sure you want to remove the role "${roleName}" from this user?`)) {
      return;
    }
    this.usersService.removeRole(userId, roleName).subscribe({
      // Optionally, you can add a success notification and refresh the list
      error: (err) => console.error('Failed to remove role', err)
    });
  }

  hasRole(user: UserProfile, roleName: 'Admin' | 'User'): boolean {
    return !!user.roles?.some(r => r.name?.toLowerCase() === roleName.toLowerCase());
  }

  onRoleSwitch(user: UserProfile, checked: boolean): void {
    const target: 'Admin' | 'User' = checked ? 'Admin' : 'User';
    const other: 'Admin' | 'User' = checked ? 'User' : 'Admin';

    const needsAssign = !this.hasRole(user, target);
    const needsRemoveOther = this.hasRole(user, other);

    if (!needsAssign && !needsRemoveOther) {
      return; // Nothing to change
    }

    const confirmMsg = checked
      ? 'Set role to Admin? This will remove User role if present.'
      : 'Set role to User? This will remove Admin role if present.';

    if (!confirm(confirmMsg)) {
      return;
    }

    const ops: Observable<unknown>[] = [];
    if (needsAssign) {
      ops.push(this.usersService.assignRole(user.id, { roleName: target }));
    }
    if (needsRemoveOther) {
      ops.push(this.usersService.removeRole(user.id, other));
    }

    forkJoin(ops).subscribe({
      // Optionally, refresh data here if needed
      error: (err) => console.error('Failed to update roles', err)
    });
  }
}
