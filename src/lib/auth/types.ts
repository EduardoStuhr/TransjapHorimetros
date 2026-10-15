export interface AuthUser {
  id: string;
  email: string;
  fullName: string;
  isActive: boolean;
  roles: string[];
  createdAt: string;
  lastLoginAt: string | null;
}

export interface BackendAuthResponse {
  accessToken: string;
  expiresAt: string;
  refreshToken: string;
  user: AuthUser;
}

export interface PublicAuthResponse {
  expiresAt: string;
  user: AuthUser;
}
