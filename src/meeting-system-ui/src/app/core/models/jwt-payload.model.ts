export interface JwtPayload {
  sub: string; // The User ID
  email: string;
  ['http://schemas.microsoft.com/ws/2008/06/identity/claims/role']: string | string[]; // The Role(s)
  exp: number;
  iss: string;
  aud: string;
}
