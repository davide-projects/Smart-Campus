// Ruoli previsti dal servizio Identity.
export type UserRole = 'Student' | 'Staff';

const ROLE_LABELS: Record<UserRole, string> = {
  Student: 'Studente',
  Staff: 'Personale',
};

export function roleLabel(role: UserRole): string {
  return ROLE_LABELS[role];
}

// Profilo restituito da POST /api/identity/login e GET /api/identity/me.
export interface UserProfile {
  id: string;
  username: string;
  displayName: string;
  role: UserRole;
}

// Risposta di POST /api/identity/login.
export interface LoginResponse {
  accessToken: string;
  tokenType: string;
  expiresAt: string;
  user: UserProfile;
}
