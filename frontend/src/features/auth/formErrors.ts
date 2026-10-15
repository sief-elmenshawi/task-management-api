import { ApiError } from '../../core/api/ApiError'
import { mapFieldErrors } from '../../shared/utils/mapFieldErrors'
import type { FieldErrors } from './types'

export type DescribedErrors = {
  fieldErrors: FieldErrors
  formError: string | null
}

export function describeApiError(error: unknown, fields: readonly string[]): DescribedErrors {
  if (!(error instanceof ApiError)) {
    return { fieldErrors: {}, formError: 'Something went wrong. Please try again.' }
  }

  const mapped = mapFieldErrors(error.fieldErrors, fields)
  const fieldErrors: FieldErrors = {}

  for (const [key, message] of Object.entries(mapped)) {
    if (fields.includes(key)) {
      fieldErrors[key] = message
    }
  }

  if (Object.keys(fieldErrors).length > 0) {
    return { fieldErrors, formError: null }
  }

  if (error.code === 'EmailAlreadyExists' && fields.includes('Email')) {
    return { fieldErrors: { Email: error.message }, formError: null }
  }

  return { fieldErrors: {}, formError: error.message }
}