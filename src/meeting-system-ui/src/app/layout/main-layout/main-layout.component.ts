import { Component, inject, signal } from '@angular/core';
import { RouterLink, RouterOutlet, RouterLinkActive, Router } from '@angular/router';
import { AuthService } from '../../core/api/auth.service';

@Component({
  selector: 'app-main-layout',
  standalone: true,
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  template: `
    <div class="main-layout">
      <!-- NEW: Overlay for mobile view when sidebar is open -->
      @if (isSidebarOpen()) {
        <div class="sidebar-overlay" (click)="toggleSidebar()"></div>
      }

      <!-- The sidebar now has a conditional 'open' class -->
      <aside class="sidebar" [class.open]="isSidebarOpen()">
        <a routerLink="/dashboard" class="logo-link">
          <img src="assets/images/meeting-system.png" alt="MeetingSys Logo" class="logo-icon" />
          <h3 class="logo-text">Meeting System</h3>
        </a>
        <nav class="nav-menu">
          <!-- Navigation links now close the sidebar on mobile -->
          <a routerLink="/dashboard" routerLinkActive="active" class="nav-link" (click)="closeSidebar()">
            <span class="nav-icon">📊</span>
            <span class="nav-text">Dashboard</span>
          </a>
          <a routerLink="/meetings" routerLinkActive="active" class="nav-link" (click)="closeSidebar()">
            <span class="nav-icon">📅</span>
            <span class="nav-text">Meetings</span>
          </a>
          <a routerLink="/users" routerLinkActive="active" class="nav-link" (click)="closeSidebar()">
            <span class="nav-icon">👥</span>
            <span class="nav-text">Users</span>
          </a>
          <a routerLink="/profile" routerLinkActive="active" class="nav-link" (click)="closeSidebar()">
            <span class="nav-icon">👤</span>
            <span class="nav-text">Profile</span>
          </a>
        </nav>

        <div class="logout-section">
          <button class="nav-link logout-button" (click)="logout()">
            <span class="nav-icon">🚪</span>
            <span class="nav-text">Logout</span>
          </button>
        </div>
      </aside>

      <div class="content-container">
        <!-- NEW: A dedicated header for the content area with the hamburger menu -->
        <header class="main-header">
          <button class="hamburger-menu" (click)="toggleSidebar()">
            <span></span>
            <span></span>
            <span></span>
          </button>
          <div class="header-title">
            <!-- This is where a dynamic page title could go in the future -->
          </div>
        </header>

        <main class="main-content">
          <router-outlet />
        </main>
      </div>
    </div>
  `,
  styles: [`
    :host {
      --sidebar-width: 250px; /* Define as a local CSS variable for the media query */
      display: block;
      height: 100%;
    }
    .main-layout {
      display: flex;
      min-height: 100vh;
      background-color: var(--background-color-light, #f8f9fa);
    }

    /* --- Mobile-First Sidebar Styles --- */
    .sidebar {
      width: var(--sidebar-width);
      background: var(--sidebar-bg, #343a40);
      color: var(--text-color-white, #fff);
      height: 100vh;
      position: fixed;
      top: 0;
      left: 0;
      z-index: 1001;
      display: flex;
      flex-direction: column;
      transform: translateX(-100%); /* Hidden by default on mobile */
      transition: transform 0.3s ease-in-out;
      padding-top: var(--spacing-base);
    }
    .sidebar.open {
      transform: translateX(0);
    }
    .logo {
      padding: 1.5rem 1rem; /* var(--spacing-lg) var(--spacing-base) */
      background-color: var(--sidebar-border-color);
      text-align: center;
    }

    .logo-link {
      display: flex;
      align-items: center;
      justify-content: center;
      text-decoration: none;
      color: var(--text-color-white, #fff);
      gap: 0.75rem; /* Space between icon and text */
      transition: opacity 0.2s;
    }

    .logo-link:hover {
      opacity: 0.9;
    }

    .logo-icon {
      height: 36px; /* Control the size of the logo */
      width: 36px;
    }

    .logo-text {
      margin: 0;
      font-size: 1.5rem;
      font-weight: 500;
    }

    .logo h3 { margin: 0; font-size: 1.5rem; }

    .nav-menu {
      flex-grow: 1; /* This is important to push the logout section down */
      overflow-y: auto;
      margin-top: 1rem;
      padding: .5rem 0;
      background: var(--sidebar-secondary-bg);
    }

    .nav-link {
      display: flex;
      align-items: center;
      padding: 0.75rem 1.5rem;
      color: var(--sidebar-text-color, #adb5bd);
      text-decoration: none;
      transition: background-color 0.2s, color 0.2s;
    }
    .nav-link:hover {
      background-color: var(--sidebar-border-color, #495057);
      color: var(--text-color-white, #fff);
    }
    .nav-link.active {
      background-color: var(--primary-color, #007bff);
      color: var(--text-color-white, #fff);
      font-weight: 500;
    }
    .nav-icon { margin-right: 0.75rem; font-size: 1.25rem; width: 24px; text-align: center; }

    .logout-section {
      padding: .5rem 0;
    }

    .logout-button {
      width: 100%;
      background: none;
      border: none;
      cursor: pointer;
      font-size: 1rem; /* Ensure font size is consistent */
    }

    .logout-button:hover {
      /* Give it a slightly different hover effect to distinguish it */
      background-color: var(--danger-color, #dc3545) !important;
      color: var(--text-color-white, #fff) !important;
    }

    /* --- Mobile-First Content Area --- */
    .content-container {
      width: 100%;
      display: flex;
      flex-direction: column;
    }
    .main-header {
      display: flex;
      align-items: center;
      padding: 0.75rem 1rem; /* var(--spacing-base) */
      background: var(--background-color-white, white);
      box-shadow: var(--box-shadow, 0 2px 4px rgba(0,0,0,0.1));
      position: sticky;
      top: 0;
      z-index: 999;
    }
    .main-content {
      flex: 1;
      padding: 1rem; /* var(--spacing-base) */
    }
    .hamburger-menu {
      display: flex;
      flex-direction: column;
      justify-content: space-around;
      width: 2rem;
      height: 2rem;
      background: transparent;
      border: none;
      cursor: pointer;
      padding: 0;
    }
    .hamburger-menu span {
      width: 2rem;
      height: 0.25rem;
      background: var(--text-color-dark, #333);
      border-radius: 10px;
    }
    .sidebar-overlay {
      position: fixed;
      top: 0;
      left: 0;
      width: 100%;
      height: 100%;
      background: rgba(0, 0, 0, 0.5);
      z-index: 1000;
    }

    /* --- Desktop Overrides --- */
    @media (min-width: 992px) {
      .sidebar {
        transform: translateX(0); /* Always visible */
      }
      .content-container {
        margin-left: var(--sidebar-width);
        width: calc(100% - var(--sidebar-width));
      }
      .main-header, .sidebar-overlay {
        display: none; /* Hide mobile-only elements */
      }
      .main-content {
        padding: 2rem; /* var(--spacing-lg) */
      }
    }
  `]
})
export class MainLayoutComponent {
  // Inject the necessary services
  private authService = inject(AuthService);
  private router = inject(Router);

  isSidebarOpen = signal(false);

  toggleSidebar(): void {
    this.isSidebarOpen.update(open => !open);
  }

  closeSidebar(): void {
    if (this.isSidebarOpen()) {
      this.isSidebarOpen.set(false);
    }
  }

  // NEW: Implement the logout method
  logout(): void {
    this.authService.logout().subscribe({
      // The 'complete' callback ensures navigation happens after the logout logic is finished.
      complete: () => {
        this.router.navigate(['/login']);
      }
    });
  }
}
