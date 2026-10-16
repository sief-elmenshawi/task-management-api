import type { AccountValues } from '../../shared/account/types'

export type UserRole = 'User' | 'Admin'

export type AuthUser = {
  id: string
  name: string
  email: string
  role: UserRole
}

export type AuthStatus = 'loading' | 'authenticated' | 'unauthenticated' | 'error'

export type AuthResponse = {
  accessToken: string
  refreshToken: string
  expiresAt: string
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

export type RegisterValues = AccountValues

export type { FieldErrors } from '../../shared/forms/formErrors'

export type AuthContextValue = {
  status: AuthStatus
  user: AuthUser | null
  login: (values: LoginValues) => Promise<void>
  register: (values: RegisterValues) => Promise<void>
  logout: () => void
  retrySession: () => void
}