import { Link } from 'react-router-dom'
import { EmptyState } from '../../../shared/components/EmptyState'
import { ErrorState } from '../../../shared/components/ErrorState'
import { Spinner } from '../../../shared/components/Spinner'
import { usePolling } from '../../../shared/hooks/usePolling'
import { formatDate } from '../../../shared/utils/formatDate'
import { PriorityBadge } from '../components/PriorityBadge'
import { StatusBadge } from '../components/StatusBadge'
import { useTasks } from '../hooks/useTasks'
import './tasks.css'

export function TasksPage() {
  const { tasks, loading, error, refetch } = useTasks()
  const hasPending = tasks.some((task) => task.status === 'Pending')

  usePolling(() => refetch(true), 2000, hasPending)

  return (
    <main className="page">
      <div className="page__header">
        <h1 className="page__title">My tasks</h1>
        <Link className="btn btn--primary" to="/tasks/new">
          New task
        </Link>
      </div>

      {loading && (
        <div className="page-loading">
          <Spinner label="Loading tasks" />
        </div>
      )}

      {!loading && error !== null && <ErrorState message={error} onRetry={() => refetch()} />}

      {!loading && error === null && tasks.length === 0 && (
        <>
          <EmptyState title="You have no tasks yet" message="Create your first task to get started." />
          <p className="task-list__cta">
            <Link className="btn btn--primary" to="/tasks/new">
              New task
            </Link>
          </p>
        </>
      )}

      {!loading && error === null && tasks.length > 0 && (
        <ul className="task-list">
          {tasks.map((task) => (
            <li key={task.id} className="task-row">
              <Link className="task-row__title" to={`/tasks/${task.id}`}>
                {task.title}
              </Link>
              {task.description && <p className="task-row__description">{task.description}</p>}
              <div className="task-row__meta">
                <PriorityBadge priority={task.priority} />
                <StatusBadge status={task.status} />
                <span className="task-row__date">{formatDate(task.createdAt)}</span>
                {task.status === 'Pending' && (
                  <span className="task-row__processing">
                    <Spinner label="Processing" />
                    Processing...
                  </span>
                )}
              </div>
            </li>
          ))}
        </ul>
      )}
    </main>
  )
}