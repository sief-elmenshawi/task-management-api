export type UserRole = 'User' | 'Admin'

export type AuthUser = {
  name: string
  role: UserRole
}

export type AuthStatus = 'loading' | 'authenticated' | 'unauthenticated'

export type AuthResponse = {
  accessToken: string
  refreshToken: string
  expiresAtUtc: string
  user: AuthUser
}

export type UserResponse = {
  id: string
  name: string
  email: string
  role: UserRole
}

export type LoginValues = {
  email: string
  password: string
}

export type RegisterValues = {
  name: string
  email: string
  password: string
  confirmPassword: string
}

export type FieldErrors = Record<string, string>

export type AuthContextValue = {
  status: AuthStatus
  user: AuthUser | null
  login: (values: LoginValues) => Promise<void>
  register: (values: RegisterValues) => Promise<void>
  logout: () => void
}