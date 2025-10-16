export interface Meeting {
  id: string;
  name?: string;
  description?: string;
  startAt: string; // ISO 8601 date string
  endAt: string;   // ISO 8601 date string
  organizerId: string;
  isCanceled: boolean;
  participants?: Participant[];
}

export interface Participant {
  userId: string;
  firstName?: string;
  lastName?: string;
  email?: string;
}

export interface CreateMeeting {
  name?: string;
  description?: string;
  startAt: string;
  endAt: string;
  participantEmails?: string[];
}

export interface UpdateMeeting {
  name?: string;
  description?: string;
  startAt: string;
  endAt: string;
  participantEmails?: string[];
}

export interface AddParticipant {
  email?: string;
}
