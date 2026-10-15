import { useState, type FormEvent } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { ApiError } from '../../../core/api/ApiError'
import { Alert } from '../../../shared/components/Alert'
import { Button } from '../../../shared/components/Button'
import { FormField } from '../../../shared/components/FormField'
import { useDocumentTitle } from '../../../shared/hooks/useDocumentTitle'
import { describeApiError } from '../../auth/formErrors'
import type { FieldErrors } from '../../auth/types'
import { usersApi } from '../api'
import type { CreateUserValues } from '../types'
import { validateCreateUser } from '../validation'
import './users.css'

const fields = ['Name', 'Email', 'Password', 'ConfirmPassword']

export function CreateUserPage() {
  useDocumentTitle('Add user')

  const navigate = useNavigate()
  const [values, setValues] = useState<CreateUserValues>({
    name: '',
    email: '',
    password: '',
    confirmPassword: '',
  })
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({})
  const [formError, setFormError] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)

  function update(field: keyof CreateUserValues, value: string) {
    setValues((current) => ({ ...current, [field]: value }))
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setFormError(null)

    const errors = validateCreateUser(values)
    setFieldErrors(errors)

    if (Object.keys(errors).length > 0) {
      return
    }

    setSubmitting(true)
    try {
      await usersApi.create(values)
      navigate('/admin/users', { state: { created: true } })
    } catch (error) {
      if (error instanceof ApiError) {
        const described = describeApiError(error, fields)
        setFieldErrors(described.fieldErrors)
        setFormError(described.formError)
      } else {
        setFormError('Something went wrong. Please try again.')
      }
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <main className="page">
      <h1 className="page__title">Add user</h1>

      {formError !== null && <Alert variant="error">{formError}</Alert>}

      <form className="user-form" onSubmit={handleSubmit} noValidate>
        <FormField id="name" label="Name" error={fieldErrors.Name}>
          <input type="text" autoComplete="name" value={values.name} onChange={(event) => update('name', event.target.value)} />
        </FormField>

        <FormField id="email" label="Email" error={fieldErrors.Email}>
          <input type="email" autoComplete="email" value={values.email} onChange={(event) => update('email', event.target.value)} />
        </FormField>

        <FormField
          id="password"
          label="Password"
          hint="At least 8 characters, with a letter and a digit."
          error={fieldErrors.Password}
        >
          <input
            type="password"
            autoComplete="new-password"
            value={values.password}
            onChange={(event) => update('password', event.target.value)}
          />
        </FormField>

        <FormField id="confirmPassword" label="Confirm password" error={fieldErrors.ConfirmPassword}>
          <input
            type="password"
            autoComplete="new-password"
            value={values.confirmPassword}
            onChange={(event) => update('confirmPassword', event.target.value)}
          />
        </FormField>

        <div className="user-form__actions">
          <Button type="submit" loading={submitting}>
            Create user
          </Button>
          <Link className="btn btn--secondary" to="/admin/users">
            Cancel
          </Link>
        </div>
      </form>
    </main>
  )
}