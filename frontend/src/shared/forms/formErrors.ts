import { ApiError } from '../../core/api/ApiError'
import { mapFieldErrors } from './mapFieldErrors'

export type FieldErrors = Record<string, string>

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

  return { fieldErrors: {}, formError: error.message }
}
