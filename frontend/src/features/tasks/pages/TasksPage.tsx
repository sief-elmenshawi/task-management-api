import { useState } from 'react'
import { Link } from 'react-router-dom'
import { EmptyState } from '../../../shared/components/EmptyState'
import { ErrorState } from '../../../shared/components/ErrorState'
import { Spinner } from '../../../shared/components/Spinner'
import { useDebouncedValue } from '../../../shared/hooks/useDebouncedValue'
import { useDocumentTitle } from '../../../shared/hooks/useDocumentTitle'
import { usePolling } from '../../../shared/hooks/usePolling'
import { formatDate } from '../../../shared/utils/formatDate'
import { PriorityBadge } from '../components/PriorityBadge'
import { StatusBadge } from '../components/StatusBadge'
import { countTasks, filterTasks, type TaskFilter } from '../filters'
import { useTasks } from '../hooks/useTasks'
import './tasks.css'

const filterLabels: Record<TaskFilter, string> = {
  All: 'All',
  Pending: 'Pending',
  InProgress: 'In Progress',
  Done: 'Done',
}

const filterOrder: readonly TaskFilter[] = ['All', 'Pending', 'InProgress', 'Done']

export function TasksPage() {
  useDocumentTitle('My tasks')

  const { tasks, loading, error, refetch } = useTasks()
  const [filter, setFilter] = useState<TaskFilter>('All')
  const [query, setQuery] = useState('')
  const debouncedQuery = useDebouncedValue(query, 250)

  const hasPending = tasks.some((task) => task.status === 'Pending')
  usePolling(() => refetch(true), 2000, hasPending)

  const counts = countTasks(tasks)
  const visibleTasks = filterTasks(tasks, filter, debouncedQuery)

  function clearFilters() {
    setFilter('All')
    setQuery('')
  }

  return (
    <main className="page">
      <div className="page__header">
        <h1 className="page__title">My tasks</h1>
        <Link className="btn btn--primary" to="/tasks/new">
          New task
        </Link>
      </div>

      <div className="task-filters">
        <div className="segmented" role="group" aria-label="Filter tasks by status">
          {filterOrder.map((option) => (
            <button
              key={option}
              type="button"
              className={option === filter ? 'segmented__item segmented__item--active' : 'segmented__item'}
              aria-pressed={option === filter}
              onClick={() => setFilter(option)}
            >
              {filterLabels[option]} ({counts[option]})
            </button>
          ))}
        </div>

        <div className="task-search">
          <label className="task-search__label" htmlFor="task-search">
            Search by title
          </label>
          <input
            id="task-search"
            type="search"
            value={query}
            placeholder="Search tasks"
            onChange={(event) => setQuery(event.target.value)}
          />
        </div>
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

      {!loading && error === null && tasks.length > 0 && visibleTasks.length === 0 && (
        <>
          <EmptyState title="No tasks match your filters" message="Try a different search or status." />
          <p className="task-list__cta">
            <button type="button" className="btn btn--secondary" onClick={clearFilters}>
              Clear filters
            </button>
          </p>
        </>
      )}

      {!loading && error === null && visibleTasks.length > 0 && (
        <ul className="task-list">
          {visibleTasks.map((task) => (
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