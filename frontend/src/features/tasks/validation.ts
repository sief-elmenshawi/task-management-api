import type { CreateTaskValues } from './types'
import { descriptionMaxLength, titleMaxLength } from './types'

export type FieldErrors = Record<string, string>

export function validateCreateTask(values: CreateTaskValues): FieldErrors {
  const errors: FieldErrors = {}

  if (values.title.trim().length === 0) {
    errors.Title = 'Title is required.'
  } else if (values.title.length > titleMaxLength) {
    errors.Title = `Title must not exceed ${titleMaxLength} characters.`
  }

  if (values.description.length > descriptionMaxLength) {
    errors.Description = `Description must not exceed ${descriptionMaxLength} characters.`
  }

  if (values.priority === '') {
    errors.Priority = 'Priority is required.'
  }

  return errors
}