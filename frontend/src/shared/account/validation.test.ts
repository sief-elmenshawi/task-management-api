import { describe, expect, it } from 'vitest'
import { validateAccount } from './validation'

const validAccount = {
  name: 'Ada Lovelace',
  email: 'ada@example.com',
  password: 'password1',
  confirmPassword: 'password1',
}

describe('validateAccount', () => {
  it('accepts valid input', () => {
    expect(validateAccount(validAccount)).toEqual({})
  })

  it('requires a name', () => {
    expect(validateAccount({ ...validAccount, name: '   ' })).toEqual({
      Name: 'Name is required.',
    })
  })

  it('rejects a name longer than 100 characters', () => {
    expect(validateAccount({ ...validAccount, name: 'a'.repeat(101) })).toEqual({
      Name: 'Name must not exceed 100 characters.',
    })
  })

  it('requires a valid email', () => {
    expect(validateAccount({ ...validAccount, email: 'not-an-email' })).toEqual({
      Email: 'Email format is invalid.',
    })
  })

  it('requires a password of at least 8 characters', () => {
    expect(
      validateAccount({ ...validAccount, password: 'short1', confirmPassword: 'short1' }),
    ).toEqual({
      Password: 'Password must be at least 8 characters long.',
    })
  })

  it('requires a letter in the password', () => {
    expect(
      validateAccount({ ...validAccount, password: '12345678', confirmPassword: '12345678' }),
    ).toEqual({
      Password: 'Password must contain at least one letter.',
    })
  })

  it('requires a digit in the password', () => {
    expect(
      validateAccount({ ...validAccount, password: 'password', confirmPassword: 'password' }),
    ).toEqual({
      Password: 'Password must contain at least one digit.',
    })
  })

  it('rejects a password longer than 72 UTF-8 bytes', () => {
    const result = validateAccount({ ...validAccount, password: `a1${'😀'.repeat(18)}` })

    expect(result.Password).toBe('Password must not exceed 72 bytes (UTF-8).')
  })

  it('requires the passwords to match', () => {
    expect(validateAccount({ ...validAccount, confirmPassword: 'different1' })).toEqual({
      ConfirmPassword: 'Passwords do not match.',
    })
  })
})
