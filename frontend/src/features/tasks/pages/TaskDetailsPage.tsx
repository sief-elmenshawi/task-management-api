import { useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { ApiError } from '../../../core/api/ApiError'
import { Alert } from '../../../shared/components/Alert'
import { Button } from '../../../shared/components/Button'
import { ErrorState } from '../../../shared/components/ErrorState'
import { Spinner } from '../../../shared/components/Spinner'
import { usePolling } from '../../../shared/hooks/usePolling'
import { formatDate } from '../../../shared/utils/formatDate'
import { tasksApi } from '../api'
import { PriorityBadge } from '../components/PriorityBadge'
import { StatusBadge } from '../components/StatusBadge'
import { useTask } from '../hooks/useTask'
import { taskStatuses } from '../types'
import type { TaskStatus } from '../types'
import './tasks.css'

const statusLabels: Record<TaskStatus, string> = {
  Pending: 'Pending',
  InProgress: 'In Progress',
  Done: 'Done',
}

export function TaskDetailsPage() {
  const { id = '' } = useParams()
  const { task, loading, error, notFound, refetch } = useTask(id)
  const [pendingStatus, setPendingStatus] = useState<TaskStatus | null>(null)
  const [statusError, setStatusError] = useState<string | null>(null)
  const updating = pendingStatus !== null

  usePolling(() => refetch(true), 2000, task?.status === 'Pending')

  if (loading) {
    return (
      <main className="page">
        <div className="page-loading">
          <Spinner label="Loading task" />
        </div>
      </main>
    )
  }

  if (notFound) {
    return (
      <main className="page state-page">
        <h1 className="page__title">Task not found</h1>
        <p className="state-page__message">This task does not exist or belongs to another user.</p>
        <Link to="/tasks">Back to tasks</Link>
      </main>
    )
  }

  if (error !== null || task === null) {
    return (
      <main className="page">
        <ErrorState message={error ?? 'Task could not be loaded.'} onRetry={() => refetch()} />
      </main>
    )
  }

  async function changeStatus(status: TaskStatus) {
    setStatusError(null)
    setPendingStatus(status)

    try {
      await tasksApi.updateStatus(id, status)
      await refetch(true)
    } catch (requestError) {
      setStatusError(
        requestError instanceof ApiError
          ? requestError.message
          : 'Something went wrong. Please try again.',
      )
    } finally {
      setPendingStatus(null)
    }
  }

  return (
    <main className="page">
      <p className="task-details__back">
        <Link to="/tasks">Back to tasks</Link>
      </p>

      <div className="page__header">
        <h1 className="page__title">{task.title}</h1>
      </div>

      <div className="task-details">
        <div className="task-details__row">
          <span className="task-details__term">Description</span>
          <p className="task-details__value">{task.description ?? '—'}</p>
        </div>
        <div className="task-details__row">
          <span className="task-details__term">Status</span>
          <span className="task-details__value">
            <StatusBadge status={task.status} />
          </span>
        </div>
        <div className="task-details__row">
          <span className="task-details__term">Priority</span>
          <span className="task-details__value">
            <PriorityBadge priority={task.priority} />
          </span>
        </div>
        <div className="task-details__row">
          <span className="task-details__term">Created</span>
          <p className="task-details__value">{formatDate(task.createdAt)}</p>
        </div>
        <div className="task-details__row">
          <span className="task-details__term">Id</span>
          <p className="task-details__value">{task.id}</p>
        </div>
      </div>

      <section className="task-details__status">
        <h2>Change status</h2>

        {task.status === 'Pending' && (
          <p className="task-details__processing">
            <Spinner label="Processing" />
            Processing...
          </p>
        )}

        {statusError !== null && <Alert variant="error">{statusError}</Alert>}

        <div className="task-details__status-control">
          {taskStatuses.map((status) => (
            <Button
              key={status}
              variant={task.status === status ? 'primary' : 'secondary'}
              disabled={updating || task.status === status}
              loading={pendingStatus === status}
              onClick={() => void changeStatus(status)}
            >
              {statusLabels[status]}
            </Button>
          ))}
        </div>
      </section>
    </main>
  )
}