import { ApiError } from '../../core/api/ApiError'
import {
  describeApiError as describeGenericApiError,
  type DescribedErrors,
} from '../../shared/forms/formErrors'

export type { DescribedErrors }

export function describeApiError(error: unknown, fields: readonly string[]): DescribedErrors {
  const described = describeGenericApiError(error, fields)

  if (described.formError === null) {
    return described
  }

  if (error instanceof ApiError && error.code === 'EmailAlreadyExists' && fields.includes('Email')) {
    return { fieldErrors: { Email: error.message }, formError: null }
  }

  return described
}
