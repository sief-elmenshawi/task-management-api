import { ApiError } from '../../core/api/ApiError'
import {
  describeApiError as describeGenericApiError,
  type DescribedErrors,
} from '../forms/formErrors'
import { ACCOUNT_FIELDS } from './validation'

export function describeAccountError(error: unknown): DescribedErrors {
  const described = describeGenericApiError(error, ACCOUNT_FIELDS)

  if (described.formError === null) {
    return described
  }

  if (error instanceof ApiError && error.code === 'EmailAlreadyExists') {
    return { fieldErrors: { Email: error.message }, formError: null }
  }

  return described
}
