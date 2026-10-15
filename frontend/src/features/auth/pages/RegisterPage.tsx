import { useState, type FormEvent } from 'react'
import { Link, Navigate, useNavigate } from 'react-router-dom'
import { Alert } from '../../../shared/components/Alert'
import { Button } from '../../../shared/components/Button'
import { FormField } from '../../../shared/components/FormField'
import { useAuth } from '../context'
import { describeApiError } from '../formErrors'
import { validateRegister } from '../validation'
import type { FieldErrors, RegisterValues } from '../types'
import './auth.css'

const fields = ['Name', 'Email', 'Password', 'ConfirmPassword']

export function RegisterPage() {
  const { status, register } = useAuth()
  const navigate = useNavigate()

  const [values, setValues] = useState<RegisterValues>({
    name: '',
    email: '',
    password: '',
    confirmPassword: '',
  })
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({})
  const [formError, setFormError] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)

  if (status === 'authenticated') {
    return <Navigate to="/tasks" replace />
  }

  function update(field: keyof RegisterValues, value: string) {
    setValues((current) => ({ ...current, [field]: value }))
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setFormError(null)

    const errors = validateRegister(values)
    setFieldErrors(errors)

    if (Object.keys(errors).length > 0) {
      return
    }

    setSubmitting(true)
    try {
      await register(values)
      navigate('/login', { state: { registered: true } })
    } catch (error) {
      const described = describeApiError(error, fields)
      setFieldErrors(described.fieldErrors)
      setFormError(described.formError)
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <main className="page">
      <div className="auth-card">
        <h1 className="page__title">Create account</h1>

        {formError && <Alert variant="error">{formError}</Alert>}

        <form className="auth-form" onSubmit={handleSubmit} noValidate>
          <FormField id="name" label="Name" error={fieldErrors.Name}>
            <input
              type="text"
              autoComplete="name"
              value={values.name}
              onChange={(event) => update('name', event.target.value)}
            />
          </FormField>

          <FormField id="email" label="Email" error={fieldErrors.Email}>
            <input
              type="email"
              autoComplete="email"
              value={values.email}
              onChange={(event) => update('email', event.target.value)}
            />
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

          <Button type="submit" loading={submitting}>
            Create account
          </Button>
        </form>

        <p className="auth-card__footer">
          Already have an account? <Link to="/login">Log in</Link>
        </p>
      </div>
    </main>
  )
}