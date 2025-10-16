export interface UserProfile {
  id: string;
  firstName?: string;
  lastName?: string;
  email?: string;
  phone?: string;
  profilePictureUrl?: string;
  roles?: Role[];
}

export interface Role {
  id: string;
  name: string;
}

export interface UpdateUserProfile {
  firstName?: string;
  lastName?: string;
  phone?: string;
}

export interface AssignRole {
  roleName?: string;
}
