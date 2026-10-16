import { useEffect, useState } from 'react'
import { Link, useLocation, useNavigate } from 'react-router-dom'
import { Alert } from '../../../shared/components/Alert'
import { Button } from '../../../shared/components/Button'
import { ConfirmDialog } from '../../../shared/components/ConfirmDialog'
import { EmptyState } from '../../../shared/components/EmptyState'
import { ErrorState } from '../../../shared/components/ErrorState'
import { Spinner } from '../../../shared/components/Spinner'
import { useDocumentTitle } from '../../../shared/hooks/useDocumentTitle'
import { formatDate } from '../../../shared/utils/formatDate'
import { useAuth } from '../../auth/context'
import { RoleBadge } from '../components/RoleBadge'
import { useUsers } from '../hooks/useUsers'
import type { UserSummary } from '../types'
import './users.css'

type LocationState = { created?: boolean } | null

export function UsersPage() {
  useDocumentTitle('Users')

  const { user: currentUser } = useAuth()
  const { users, loading, error, refetch, removing, removeError, remove } = useUsers()
  const navigate = useNavigate()
  const location = useLocation()

  const [notice] = useState(() => {
    const state = location.state as LocationState
    return { created: state?.created === true }
  })
  const [pendingDelete, setPendingDelete] = useState<UserSummary | null>(null)

  useEffect(() => {
    if (location.state !== null) {
      navigate(location.pathname, { replace: true, state: null })
    }
  }, [location, navigate])

  async function confirmDelete() {
    if (pendingDelete === null) {
      return
    }

    const completed = await remove(pendingDelete.id)

    if (completed) {
      setPendingDelete(null)
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
      {removeError !== null && <Alert variant="error">{removeError}</Alert>}

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
        confirmLabel={removing ? 'Deleting...' : 'Delete'}
        busy={removing}
        onCancel={() => setPendingDelete(null)}
        onConfirm={() => void confirmDelete()}
      />
    </main>
  )
}