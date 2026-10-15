import { useEffect, useState, type FormEvent } from 'react'
import { Link, Navigate, useLocation, useNavigate } from 'react-router-dom'
import { Alert } from '../../../shared/components/Alert'
import { Button } from '../../../shared/components/Button'
import { FormField } from '../../../shared/components/FormField'
import { useDocumentTitle } from '../../../shared/hooks/useDocumentTitle'
import { useAuth } from '../context'
import { describeApiError } from '../formErrors'
import { validateLogin } from '../validation'
import type { FieldErrors } from '../types'
import './auth.css'

type LocationState = {
  from?: { pathname?: string }
  sessionExpired?: boolean
  registered?: boolean
}

export function LoginPage() {
  useDocumentTitle('Log in')

  const { status, login } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()

  const [notice] = useState(() => {
    const state = (location.state ?? null) as LocationState | null

    return {
      sessionExpired: state?.sessionExpired === true,
      registered: state?.registered === true,
      redirectTo: state?.from?.pathname ?? '/tasks',
    }
  })

  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({})
  const [formError, setFormError] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)

  useEffect(() => {
    if (location.state !== null) {
      navigate(location.pathname, { replace: true, state: null })
    }
  }, [location, navigate])

  if (status === 'authenticated') {
    return <Navigate to={notice.redirectTo} replace />
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setFormError(null)

    const values = { email, password }
    const errors = validateLogin(values)
    setFieldErrors(errors)

    if (Object.keys(errors).length > 0) {
      return
    }

    setSubmitting(true)
    try {
      await login(values)
    } catch (error) {
      const described = describeApiError(error, ['Email', 'Password'])
      setFieldErrors(described.fieldErrors)
      setFormError(described.formError)
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <main className="page auth-page">
      <div className="auth-card">
        <h1 className="page__title">Log in</h1>
        <p className="auth-card__lead">Sign in to manage your tasks.</p>

        {notice.sessionExpired && <Alert variant="info">Your session expired. Please sign in again.</Alert>}
        {notice.registered && <Alert variant="success">Account created. You can sign in now.</Alert>}
        {formError && <Alert variant="error">{formError}</Alert>}

        <form className="auth-form" onSubmit={handleSubmit} noValidate>
          <FormField id="login-email" label="Email" error={fieldErrors.Email}>
            <input
              type="email"
              autoComplete="email"
              value={email}
              onChange={(event) => setEmail(event.target.value)}
            />
          </FormField>

          <FormField id="login-password" label="Password" error={fieldErrors.Password}>
            <input
              type="password"
              autoComplete="current-password"
              value={password}
              onChange={(event) => setPassword(event.target.value)}
            />
          </FormField>

          <Button type="submit" loading={submitting}>
            Log in
          </Button>
        </form>

        <p className="auth-card__footer">
          No account yet? <Link to="/register">Create one</Link>
        </p>
      </div>
    </main>
  )
}