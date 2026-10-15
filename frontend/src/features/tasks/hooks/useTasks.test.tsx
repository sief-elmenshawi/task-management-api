import { act, render, screen, waitFor } from '@testing-library/react'
import { useEffect } from 'react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ApiError } from '../../../core/api/ApiError'
import { tasksApi } from '../api'
import type { Task } from '../types'
import { useTasks, type UseTasksResult } from './useTasks'

vi.mock('../api', () => ({
  tasksApi: { list: vi.fn() },
}))

type Refetch = UseTasksResult['refetch']

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

function Probe({ onReady }: { onReady: (refetch: Refetch) => void }) {
  const { tasks, loading, error, refetch } = useTasks()

  useEffect(() => {
    onReady(refetch)
  }, [onReady, refetch])

  return (
    <div>
      <span data-testid="titles">{tasks.map((task) => task.title).join(',')}</span>
      <span data-testid="error">{error ?? 'none'}</span>
      <span data-testid="loading">{String(loading)}</span>
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

describe('useTasks', () => {
  beforeEach(() => {
    vi.mocked(tasksApi.list).mockReset()
  })

  it('keeps the last good data and skips the error when a silent refetch fails', async () => {
    vi.mocked(tasksApi.list).mockResolvedValueOnce([makeTask()])
    const { handle, onReady } = createHandle()
    render(<Probe onReady={onReady} />)

    await waitFor(() => expect(screen.getByTestId('titles')).toHaveTextContent('Task'))

    vi.mocked(tasksApi.list).mockRejectedValueOnce(networkError())
    await act(async () => {
      void handle.refetch?.(true)
    })
    await waitFor(() => expect(tasksApi.list).toHaveBeenCalledTimes(2))

    expect(screen.getByTestId('titles')).toHaveTextContent('Task')
    expect(screen.getByTestId('error')).toHaveTextContent('none')
    expect(screen.getByTestId('loading')).toHaveTextContent('false')
  })

  it('surfaces the error when a non-silent refetch fails', async () => {
    vi.mocked(tasksApi.list).mockResolvedValueOnce([makeTask()])
    const { handle, onReady } = createHandle()
    render(<Probe onReady={onReady} />)

    await waitFor(() => expect(screen.getByTestId('titles')).toHaveTextContent('Task'))

    vi.mocked(tasksApi.list).mockRejectedValueOnce(networkError())
    await act(async () => {
      void handle.refetch?.()
    })
    await waitFor(() => expect(tasksApi.list).toHaveBeenCalledTimes(2))

    expect(screen.getByTestId('error')).toHaveTextContent('Cannot reach the server')
    expect(screen.getByTestId('loading')).toHaveTextContent('false')
  })
})
