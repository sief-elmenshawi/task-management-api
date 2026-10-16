import { describe, expect, it } from 'vitest'
import { mapFieldErrors } from './mapFieldErrors'

describe('mapFieldErrors', () => {
  it('maps API error keys to form fields case-insensitively', () => {
    const result = mapFieldErrors(
      {
        Status: ['Status is required.'],
        Email: ['Email format is invalid.', 'Email must not exceed 256 characters.'],
      },
      ['status', 'email'],
    )

    expect(result).toEqual({
      status: 'Status is required.',
      email: 'Email format is invalid.',
    })
  })

  it('keeps keys that match no form field', () => {
    const result = mapFieldErrors({ request: ['A request body is required.'] }, ['status'])

    expect(result).toEqual({ request: 'A request body is required.' })
  })
})
