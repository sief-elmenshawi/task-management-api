import { act, render, screen, waitFor } from '@testing-library/react'
import { useEffect } from 'react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ApiError } from '../../../core/api/ApiError'
import { tasksApi } from '../api'
import type { Task } from '../types'
import { useTask, type UseTaskResult } from './useTask'

vi.mock('../api', () => ({
  tasksApi: { getById: vi.fn() },
}))

type Refetch = UseTaskResult['refetch']

function makeTask(overrides: Partial<Task> = {}): Task {
  return {
    id: 'task-id',
    title: 'Task',
    description: null,
    status: 'Pending',
    priority: 'Medium',
    createdAt: '2026-01-01T00:00:00Z',
    ...overrides,
  }
}

function networkError(): ApiError {
  return new ApiError({ status: 0, code: 'NetworkError', message: 'Cannot reach the server' })
}

function notFoundError(): ApiError {
  return new ApiError({ status: 404, code: 'NotFound', message: 'Task not found.' })
}

function Probe({ onReady }: { onReady: (refetch: Refetch) => void }) {
  const { task, error, notFound, refetch } = useTask('task-id')

  useEffect(() => {
    onReady(refetch)
  }, [onReady, refetch])

  return (
    <div>
      <span data-testid="title">{task?.title ?? 'none'}</span>
      <span data-testid="error">{error ?? 'none'}</span>
      <span data-testid="notFound">{String(notFound)}</span>
    </div>
  )
}

function createHandle(): { handle: { refetch: Refetch | null }; onReady: (refetch: Refetch) => void } {
  const handle: { refetch: Refetch | null } = { refetch: null }
  const onReady = (refetch: Refetch) => {
    handle.refetch = refetch
  }

  return { handle, onReady }
}

describe('useTask', () => {
  beforeEach(() => {
    vi.mocked(tasksApi.getById).mockReset()
  })

  it('keeps the last good task and skips the error when a silent refetch fails', async () => {
    vi.mocked(tasksApi.getById).mockResolvedValueOnce(makeTask())
    const { handle, onReady } = createHandle()
    render(<Probe onReady={onReady} />)

    await waitFor(() => expect(screen.getByTestId('title')).toHaveTextContent('Task'))

    vi.mocked(tasksApi.getById).mockRejectedValueOnce(networkError())
    await act(async () => {
      void handle.refetch?.(true)
    })
    await waitFor(() => expect(tasksApi.getById).toHaveBeenCalledTimes(2))

    expect(screen.getByTestId('title')).toHaveTextContent('Task')
    expect(screen.getByTestId('error')).toHaveTextContent('none')
    expect(screen.getByTestId('notFound')).toHaveTextContent('false')
  })

  it('does not mark a task missing when a silent refetch answers 404', async () => {
    vi.mocked(tasksApi.getById).mockResolvedValueOnce(makeTask())
    const { handle, onReady } = createHandle()
    render(<Probe onReady={onReady} />)

    await waitFor(() => expect(screen.getByTestId('title')).toHaveTextContent('Task'))

    vi.mocked(tasksApi.getById).mockRejectedValueOnce(notFoundError())
    await act(async () => {
      void handle.refetch?.(true)
    })
    await waitFor(() => expect(tasksApi.getById).toHaveBeenCalledTimes(2))

    expect(screen.getByTestId('notFound')).toHaveTextContent('false')
    expect(screen.getByTestId('error')).toHaveTextContent('none')
  })

  it('surfaces an error when a non-silent refetch fails', async () => {
    vi.mocked(tasksApi.getById).mockResolvedValueOnce(makeTask())
    const { handle, onReady } = createHandle()
    render(<Probe onReady={onReady} />)

    await waitFor(() => expect(screen.getByTestId('title')).toHaveTextContent('Task'))

    vi.mocked(tasksApi.getById).mockRejectedValueOnce(networkError())
    await act(async () => {
      void handle.refetch?.()
    })
    await waitFor(() => expect(tasksApi.getById).toHaveBeenCalledTimes(2))

    expect(screen.getByTestId('error')).toHaveTextContent('Cannot reach the server')
  })

  it('marks the task missing when a non-silent refetch answers 404', async () => {
    vi.mocked(tasksApi.getById).mockResolvedValueOnce(makeTask())
    const { handle, onReady } = createHandle()
    render(<Probe onReady={onReady} />)

    await waitFor(() => expect(screen.getByTestId('title')).toHaveTextContent('Task'))

    vi.mocked(tasksApi.getById).mockRejectedValueOnce(notFoundError())
    await act(async () => {
      void handle.refetch?.()
    })
    await waitFor(() => expect(tasksApi.getById).toHaveBeenCalledTimes(2))

    expect(screen.getByTestId('notFound')).toHaveTextContent('true')
  })
})
