import { describe, expect, it } from 'vitest'
import { validateAccount } from '../../shared/account/validation'
import { validateCreateUser } from './validation'

describe('validateCreateUser', () => {
  it('delegates account-creation validation to the shared owner', () => {
    expect(validateCreateUser).toBe(validateAccount)
  })
})
