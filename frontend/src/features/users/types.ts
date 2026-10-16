import type { UserRole } from '../auth/types'

export type UserSummary = {
  id: string
  name: string
  email: string
  role: UserRole
  createdAt: string
}

export type { AccountValues as CreateUserValues } from '../../shared/account/types'