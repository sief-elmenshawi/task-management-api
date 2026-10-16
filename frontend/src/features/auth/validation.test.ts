import { describe, expect, it } from 'vitest'
import { validateAccount } from '../../shared/account/validation'
import { validateLogin, validateRegister } from './validation'

describe('validateRegister', () => {
  it('delegates account-creation validation to the shared owner', () => {
    expect(validateRegister).toBe(validateAccount)
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
