import type { FieldErrors, LoginValues } from './types'

export { validateAccount as validateRegister } from '../../shared/account/validation'

export function validateLogin(values: LoginValues): FieldErrors {
  const errors: FieldErrors = {}

  if (!values.email || values.email.trim().length === 0) {
    errors.Email = 'Email is required.'
  } else {
    const emailPattern = /^[^\s@]+@[^\s@]+\.[^\s@]+$/
    if (!emailPattern.test(values.email)) {
      errors.Email = 'Email format is invalid.'
    }
  }

  if (!values.password || values.password.length === 0) {
    errors.Password = 'Password is required.'
  } else {
    const utf8Length = new TextEncoder().encode(values.password).length
    if (utf8Length > 72) {
      errors.Password = 'Password must not exceed 72 bytes (UTF-8).'
    }
  }

  return errors
}
