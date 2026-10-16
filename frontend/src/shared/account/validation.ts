import type { FieldErrors } from '../forms/formErrors'
import type { AccountValues } from './types'

export const ACCOUNT_FIELDS: readonly string[] = ['Name', 'Email', 'Password', 'ConfirmPassword']

export function validateAccount(values: AccountValues): FieldErrors {
  const errors: FieldErrors = {}

  if (!values.name || values.name.trim().length === 0) {
    errors.Name = 'Name is required.'
  } else if (values.name.length > 100) {
    errors.Name = 'Name must not exceed 100 characters.'
  }

  if (!values.email || values.email.trim().length === 0) {
    errors.Email = 'Email is required.'
  } else {
    const emailPattern = /^[^\s@]+@[^\s@]+\.[^\s@]+$/
    if (!emailPattern.test(values.email)) {
      errors.Email = 'Email format is invalid.'
    } else if (values.email.length > 256) {
      errors.Email = 'Email must not exceed 256 characters.'
    }
  }

  if (!values.password || values.password.length === 0) {
    errors.Password = 'Password is required.'
  } else {
    const utf8Length = new TextEncoder().encode(values.password).length
    if (utf8Length > 72) {
      errors.Password = 'Password must not exceed 72 bytes (UTF-8).'
    } else if (values.password.length < 8) {
      errors.Password = 'Password must be at least 8 characters long.'
    } else if (values.password.length > 72) {
      errors.Password = 'Password must not exceed 72 characters.'
    } else if (!/[A-Za-z]/.test(values.password)) {
      errors.Password = 'Password must contain at least one letter.'
    } else if (!/[0-9]/.test(values.password)) {
      errors.Password = 'Password must contain at least one digit.'
    }
  }

  if (!values.confirmPassword || values.confirmPassword.length === 0) {
    errors.ConfirmPassword = 'Confirm password is required.'
  } else if (values.confirmPassword !== values.password) {
    errors.ConfirmPassword = 'Passwords do not match.'
  }

  return errors
}
