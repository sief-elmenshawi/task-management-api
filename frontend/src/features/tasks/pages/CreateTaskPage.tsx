import { useState, type FormEvent } from 'react'
import { useNavigate } from 'react-router-dom'
import { ApiError } from '../../../core/api/ApiError'
import { Alert } from '../../../shared/components/Alert'
import { Button } from '../../../shared/components/Button'
import { FormField } from '../../../shared/components/FormField'
import { useDocumentTitle } from '../../../shared/hooks/useDocumentTitle'
import { mapFieldErrors } from '../../../shared/utils/mapFieldErrors'
import { tasksApi } from '../api'
import { descriptionMaxLength, taskPriorities, titleMaxLength } from '../types'
import type { CreateTaskValues, TaskPriority } from '../types'
import { validateCreateTask, type FieldErrors } from '../validation'
import './tasks.css'

const fields = ['Title', 'Description', 'Priority']

const priorityLabels: Record<TaskPriority, string> = {
  Low: 'Low',
  Medium: 'Medium',
  High: 'High',
}

export function CreateTaskPage() {
  useDocumentTitle('New task')

  const navigate = useNavigate()
  const [values, setValues] = useState<CreateTaskValues>({
    title: '',
    description: '',
    priority: '',
  })
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({})
  const [formError, setFormError] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)

  function update(field: keyof CreateTaskValues, value: string) {
    setValues((current) => ({ ...current, [field]: value }))
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setFormError(null)

    const errors = validateCreateTask(values)
    setFieldErrors(errors)

    if (Object.keys(errors).length > 0) {
      return
    }

    setSubmitting(true)
    try {
      await tasksApi.create({
        title: values.title.trim(),
        description: values.description.trim() === '' ? null : values.description,
        priority: values.priority as TaskPriority,
      })
      navigate('/tasks')
    } catch (error) {
      if (error instanceof ApiError) {
        setFieldErrors(mapFieldErrors(error.fieldErrors, fields))
        setFormError(error.message)
      } else {
        setFormError('Something went wrong. Please try again.')
      }
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <main className="page">
      <h1 className="page__title">New task</h1>

      {formError !== null && <Alert variant="error">{formError}</Alert>}

      <form className="task-form" onSubmit={handleSubmit} noValidate>
        <FormField id="title" label="Title" error={fieldErrors.Title}>
          <input
            type="text"
            value={values.title}
            onChange={(event) => update('title', event.target.value)}
          />
        </FormField>
        <p className="task-form__counter" id="title-counter">
          {values.title.length} / {titleMaxLength}
        </p>

        <FormField id="description" label="Description" error={fieldErrors.Description}>
          <textarea
            rows={4}
            value={values.description}
            onChange={(event) => update('description', event.target.value)}
          />
        </FormField>
        <p className="task-form__counter" id="description-counter">
          {values.description.length} / {descriptionMaxLength}
        </p>

        <FormField id="priority" label="Priority" error={fieldErrors.Priority}>
          <select value={values.priority} onChange={(event) => update('priority', event.target.value)}>
            <option value="">Select a priority</option>
            {taskPriorities.map((priority) => (
              <option key={priority} value={priority}>
                {priorityLabels[priority]}
              </option>
            ))}
          </select>
        </FormField>

        <Button type="submit" loading={submitting}>
          Create task
        </Button>
      </form>
    </main>
  )
}