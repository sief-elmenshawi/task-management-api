import { useEffect, useRef, useState } from 'react'
import { Link, useLocation, useNavigate } from 'react-router-dom'
import { ApiError } from '../../../core/api/ApiError'
import { Alert } from '../../../shared/components/Alert'
import { Button } from '../../../shared/components/Button'
import { ConfirmDialog } from '../../../shared/components/ConfirmDialog'
import { EmptyState } from '../../../shared/components/EmptyState'
import { ErrorState } from '../../../shared/components/ErrorState'
import { Spinner } from '../../../shared/components/Spinner'
import { useDocumentTitle } from '../../../shared/hooks/useDocumentTitle'
import { formatDate } from '../../../shared/utils/formatDate'
import { useAuth } from '../../auth/context'
import { usersApi } from '../api'
import { RoleBadge } from '../components/RoleBadge'
import { useUsers } from '../hooks/useUsers'
import type { UserSummary } from '../types'
import './users.css'

type LocationState = { created?: boolean } | null

export function UsersPage() {
  useDocumentTitle('Users')

  const { user: currentUser } = useAuth()
  const { users, loading, error, refetch } = useUsers()
  const navigate = useNavigate()
  const location = useLocation()

  const [notice] = useState(() => {
    const state = location.state as LocationState
    return { created: state?.created === true }
  })
  const [pendingDelete, setPendingDelete] = useState<UserSummary | null>(null)
  const [deleting, setDeleting] = useState(false)
  const [actionError, setActionError] = useState<string | null>(null)
  const deleteRequestRef = useRef(0)

  useEffect(() => {
    if (location.state !== null) {
      navigate(location.pathname, { replace: true, state: null })
    }
  }, [location, navigate])

  async function confirmDelete() {
    if (pendingDelete === null || deleting) {
      return
    }

    const target = pendingDelete
    const requestId = deleteRequestRef.current + 1
    deleteRequestRef.current = requestId
    setActionError(null)
    setDeleting(true)

    try {
      await usersApi.remove(target.id)
      if (deleteRequestRef.current !== requestId) {
        return
      }
      refetch()
    } catch (requestError) {
      if (deleteRequestRef.current !== requestId) {
        return
      }
      setActionError(
        requestError instanceof ApiError
          ? requestError.message
          : 'Something went wrong. Please try again.',
      )
    } finally {
      if (deleteRequestRef.current === requestId) {
        setDeleting(false)
        setPendingDelete(null)
      }
    }
  }

  return (
    <main className="page">
      <div className="page__header">
        <h1 className="page__title">Users</h1>
        <Link className="btn btn--primary" to="/admin/users/new">
          Add user
        </Link>
      </div>

      {notice.created && <Alert variant="success">The user was created.</Alert>}
      {actionError !== null && <Alert variant="error">{actionError}</Alert>}

      {loading && (
        <div className="page-loading">
          <Spinner label="Loading users" />
        </div>
      )}

      {!loading && error !== null && <ErrorState message={error} onRetry={refetch} />}

      {!loading && error === null && users.length === 0 && (
        <EmptyState title="No users yet" message="Add the first user to get started." />
      )}

      {!loading && error === null && users.length > 0 && (
        <table className="users-table">
          <thead>
            <tr>
              <th scope="col">Name</th>
              <th scope="col">Email</th>
              <th scope="col">Role</th>
              <th scope="col">Created</th>
              <th scope="col">
                <span className="visually-hidden">Actions</span>
              </th>
            </tr>
          </thead>
          <tbody>
            {users.map((user) => {
              const isSelf = currentUser?.id === user.id

              return (
                <tr key={user.id}>
                  <td data-label="Name">{user.name}</td>
                  <td data-label="Email">{user.email}</td>
                  <td data-label="Role">
                    <RoleBadge role={user.role} />
                  </td>
                  <td data-label="Created">{formatDate(user.createdAt)}</td>
                  <td className="users-table__actions">
                    <Button
                      variant="danger"
                      disabled={isSelf}
                      onClick={() => setPendingDelete(user)}
                    >
                      Delete
                    </Button>
                  </td>
                </tr>
              )
            })}
          </tbody>
        </table>
      )}

      <ConfirmDialog
        open={pendingDelete !== null}
        title="Delete user"
        message={
          pendingDelete === null
            ? ''
            : `Delete ${pendingDelete.name} (${pendingDelete.email})? This cannot be undone.`
        }
        confirmLabel={deleting ? 'Deleting...' : 'Delete'}
        busy={deleting}
        onCancel={() => setPendingDelete(null)}
        onConfirm={() => void confirmDelete()}
      />
    </main>
  )
}