import { Routes } from '@angular/router';
import { LoginComponent } from './features/auth/login/login.component';
import { RegisterComponent } from './features/auth/register/register.component';
import { DashboardComponent } from './features/dashboard/dashboard.component';
import { authGuard } from './features/auth/guards/auth.guard';
// Import the MainLayoutComponent
import { MainLayoutComponent } from './layout/main-layout/main-layout.component';

export const routes: Routes = [
  // Auth routes are outside the main layout
  { path: 'login', component: LoginComponent },
  { path: 'register', component: RegisterComponent },

  // This is the main layout route for all authenticated pages
  {
    path: '',
    component: MainLayoutComponent,
    canActivate: [authGuard], // Protect the entire layout
    children: [
      { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
      { path: 'dashboard', component: DashboardComponent },
      {
        path: 'meetings',
        loadChildren: () => import('./features/meetings/meetings.routes').then(m => m.MEETINGS_ROUTES)
      },
      {
        path: 'users',
        loadChildren: () => import('./features/users/users.routes').then(m => m.USERS_ROUTES)
      },
      {
        path: 'profile',
        loadChildren: () => import('./features/user-profile/user-profile.routes').then(m => m.USER_PROFILE_ROUTES)
      }
    ]
  },

  // Wildcard route should be at the end
  { path: '**', redirectTo: '' }
];
