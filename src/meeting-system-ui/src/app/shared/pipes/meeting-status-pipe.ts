import { Pipe, PipeTransform } from '@angular/core';
import { Meeting } from '../../core/models/meeting.model';

@Pipe({
  name: 'meetingStatus',
  standalone: true,
})
export class MeetingStatusPipe implements PipeTransform {
  transform(meeting: Meeting): string {
    if (!meeting) {
      return '';
    }

    if (meeting.isCanceled) {
      return 'Canceled';
    }

    const now = new Date();
    const startAt = new Date(meeting.startAt);
    const endAt = new Date(meeting.endAt);

    if (now < startAt) {
      return 'Upcoming';
    } else if (now >= startAt && now <= endAt) {
      return 'In Progress';
    } else {
      return 'Finished';
    }
  }
}
