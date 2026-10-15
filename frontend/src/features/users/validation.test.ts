import { describe, expect, it } from 'vitest'
import { validateCreateUser } from './validation'

const validUser = {
  name: 'Ada Lovelace',
  email: 'ada@example.com',
  password: 'password1',
  confirmPassword: 'password1',
}

describe('validateCreateUser', () => {
  it('accepts valid input', () => {
    expect(validateCreateUser(validUser)).toEqual({})
  })

  it('requires a name', () => {
    expect(validateCreateUser({ ...validUser, name: '   ' })).toEqual({
      Name: 'Name is required.',
    })
  })

  it('rejects an invalid email format', () => {
    expect(validateCreateUser({ ...validUser, email: 'nope' })).toEqual({
      Email: 'Email format is invalid.',
    })
  })

  it('requires a password of at least 8 characters', () => {
    expect(
      validateCreateUser({ ...validUser, password: 'short1', confirmPassword: 'short1' }),
    ).toEqual({
      Password: 'Password must be at least 8 characters long.',
    })
  })

  it('requires the passwords to match', () => {
    expect(validateCreateUser({ ...validUser, confirmPassword: 'different1' })).toEqual({
      ConfirmPassword: 'Passwords do not match.',
    })
  })
})
