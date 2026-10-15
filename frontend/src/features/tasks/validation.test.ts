import { describe, expect, it } from 'vitest'
import { validateCreateTask } from './validation'
import type { CreateTaskValues } from './types'

const validTask: CreateTaskValues = {
  title: 'Write the report',
  description: 'Summarise the quarter.',
  priority: 'High',
}

describe('validateCreateTask', () => {
  it('accepts valid input', () => {
    expect(validateCreateTask(validTask)).toEqual({})
  })

  it('requires a title that is not only whitespace', () => {
    expect(validateCreateTask({ ...validTask, title: '   ' })).toEqual({
      Title: 'Title is required.',
    })
  })

  it('rejects a title longer than 200 characters', () => {
    expect(validateCreateTask({ ...validTask, title: 'a'.repeat(201) })).toEqual({
      Title: 'Title must not exceed 200 characters.',
    })
  })

  it('rejects a description longer than 1000 characters', () => {
    expect(validateCreateTask({ ...validTask, description: 'a'.repeat(1001) })).toEqual({
      Description: 'Description must not exceed 1000 characters.',
    })
  })

  it('accepts a title of exactly 200 characters', () => {
    expect(validateCreateTask({ ...validTask, title: 'a'.repeat(200) })).toEqual({})
  })

  it('requires a priority', () => {
    expect(validateCreateTask({ ...validTask, priority: '' })).toEqual({
      Priority: 'Priority is required.',
    })
  })
})