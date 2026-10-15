import type { UserRole } from '../auth/types'

export type UserSummary = {
  id: string
  name: string
  email: string
  role: UserRole
  createdAt: string
}

export type CreateUserValues = {
  name: string
  email: string
  password: string
  confirmPassword: string
}