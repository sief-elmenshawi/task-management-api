import { describe, expect, it } from 'vitest'
import { validateLogin, validateRegister } from './validation'

const validRegistration = {
  name: 'Ada Lovelace',
  email: 'ada@example.com',
  password: 'password1',
  confirmPassword: 'password1',
}

describe('validateRegister', () => {
  it('accepts valid input', () => {
    expect(validateRegister(validRegistration)).toEqual({})
  })

  it('requires a name', () => {
    expect(validateRegister({ ...validRegistration, name: '   ' })).toEqual({
      Name: 'Name is required.',
    })
  })

  it('rejects a name longer than 100 characters', () => {
    expect(validateRegister({ ...validRegistration, name: 'a'.repeat(101) })).toEqual({
      Name: 'Name must not exceed 100 characters.',
    })
  })

  it('requires a valid email', () => {
    expect(validateRegister({ ...validRegistration, email: 'not-an-email' })).toEqual({
      Email: 'Email format is invalid.',
    })
  })

  it('requires a password of at least 8 characters', () => {
    expect(
      validateRegister({ ...validRegistration, password: 'short1', confirmPassword: 'short1' }),
    ).toEqual({
      Password: 'Password must be at least 8 characters long.',
    })
  })

  it('requires a letter in the password', () => {
    expect(
      validateRegister({ ...validRegistration, password: '12345678', confirmPassword: '12345678' }),
    ).toEqual({
      Password: 'Password must contain at least one letter.',
    })
  })

  it('requires a digit in the password', () => {
    expect(
      validateRegister({ ...validRegistration, password: 'password', confirmPassword: 'password' }),
    ).toEqual({
      Password: 'Password must contain at least one digit.',
    })
  })

  it('rejects a password longer than 72 UTF-8 bytes', () => {
    const result = validateRegister({ ...validRegistration, password: `a1${'😀'.repeat(18)}` })

    expect(result.Password).toBe('Password must not exceed 72 bytes (UTF-8).')
  })

  it('requires the passwords to match', () => {
    expect(validateRegister({ ...validRegistration, confirmPassword: 'different1' })).toEqual({
      ConfirmPassword: 'Passwords do not match.',
    })
  })
})

describe('validateLogin', () => {
  it('accepts valid input', () => {
    expect(validateLogin({ email: 'ada@example.com', password: 'password1' })).toEqual({})
  })

  it('requires an email', () => {
    expect(validateLogin({ email: '', password: 'password1' })).toEqual({
      Email: 'Email is required.',
    })
  })

  it('rejects an invalid email format', () => {
    expect(validateLogin({ email: 'nope', password: 'password1' })).toEqual({
      Email: 'Email format is invalid.',
    })
  })

  it('requires a password', () => {
    expect(validateLogin({ email: 'ada@example.com', password: '' })).toEqual({
      Password: 'Password is required.',
    })
  })
})