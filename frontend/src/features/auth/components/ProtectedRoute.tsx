import type { ReactNode } from 'react'
import { Navigate, useLocation } from 'react-router-dom'
import { ErrorState } from '../../../shared/components/ErrorState'
import { Spinner } from '../../../shared/components/Spinner'
import { useAuth } from '../context'
import { getLoginRedirect } from '../loginRedirect'

export function ProtectedRoute({ children }: { children: ReactNode }) {
  const { status, retrySession } = useAuth()
  const location = useLocation()

  if (status === 'loading') {
    return (
      <div className="page-loading">
        <Spinner label="Loading your session" />
      </div>
    )
  }

  if (status === 'error') {
    return (
      <main className="page">
        <ErrorState
          message="We could not reach the server to verify your session. Check your connection and try again."
          onRetry={retrySession}
        />
      </main>
    )
  }

  if (status === 'unauthenticated') {
    const redirect = getLoginRedirect()

    if (redirect !== null) {
      return <Navigate to="/login" state={redirect} replace />
    }

    return <Navigate to="/login" state={{ from: location }} replace />
  }

  return <>{children}</>
}