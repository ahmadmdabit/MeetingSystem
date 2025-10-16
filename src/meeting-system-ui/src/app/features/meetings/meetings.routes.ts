import { Routes } from '@angular/router';
import { MeetingsListComponent } from './list/meetings-list.component';
import { MeetingDetailComponent } from './detail/meeting-detail.component';
// Import the form component directly
import { MeetingFormComponent } from './form/meeting-form.component';

export const MEETINGS_ROUTES: Routes = [
  { path: '', component: MeetingsListComponent },
  {
    path: 'new',
    // The form component will see no 'id' in the URL and will run in "create" mode.
    component: MeetingFormComponent
  },
  { path: ':id', component: MeetingDetailComponent },
  {
    path: ':id/edit',
    // The form component will see an 'id' in the URL and will run in "edit" mode.
    component: MeetingFormComponent
  }
];
