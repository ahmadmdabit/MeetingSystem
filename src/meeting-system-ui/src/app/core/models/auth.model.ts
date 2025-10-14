export interface Login {
  email?: string;
  password?: string;
}

export interface RegisterUser {
  firstName?: string;
  lastName?: string;
  email?: string;
  phone?: string;
  password?: string;
  profilePicture?: File; // Represent binary as File for uploads
}

// The login response is not defined in swagger, assuming it returns a token
export interface AuthResponse {
  token: string;
}
