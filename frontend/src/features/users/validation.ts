import type { FieldErrors } from '../auth/types'
import { validateRegister } from '../auth/validation'
import type { CreateUserValues } from './types'

export function validateCreateUser(values: CreateUserValues): FieldErrors {
  return validateRegister(values)
}