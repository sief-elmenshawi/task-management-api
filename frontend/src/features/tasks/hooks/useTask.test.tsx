import { act, render, screen, waitFor } from '@testing-library/react'
import { useEffect } from 'react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ApiError } from '../../../core/api/ApiError'
import { tasksApi } from '../api'
import type { Task } from '../types'
import { useTask, type UseTaskResult } from './useTask'

vi.mock('../api', () => ({
  tasksApi: { getById: vi.fn(), updateStatus: vi.fn() },
}))

type Refetch = UseTaskResult['refetch']
type ChangeStatus = UseTaskResult['changeStatus']

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

type ApiHandle = { refetch: Refetch; changeStatus: ChangeStatus }

function Probe({ onReady }: { onReady: (handle: ApiHandle) => void }) {
  const { task, error, notFound, refetch, changeStatus } = useTask('task-id')

  useEffect(() => {
    onReady({ refetch, changeStatus })
  }, [onReady, refetch, changeStatus])

  return (
    <div>
      <span data-testid="title">{task?.title ?? 'none'}</span>
      <span data-testid="status">{task?.status ?? 'none'}</span>
      <span data-testid="error">{error ?? 'none'}</span>
      <span data-testid="notFound">{String(notFound)}</span>
    </div>
  )
}

function createHandle(): { handle: { refetch: Refetch | null; changeStatus: ChangeStatus | null }; onReady: (handle: ApiHandle) => void } {
  const handle: { refetch: Refetch | null; changeStatus: ChangeStatus | null } = {
    refetch: null,
    changeStatus: null,
  }
  const onReady = (api: ApiHandle) => {
    handle.refetch = api.refetch
    handle.changeStatus = api.changeStatus
  }

  return { handle, onReady }
}

describe('useTask', () => {
  beforeEach(() => {
    vi.mocked(tasksApi.getById).mockReset()
    vi.mocked(tasksApi.updateStatus).mockReset()
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

  it('shows the mutation result even when the follow-up refresh fails', async () => {
    vi.mocked(tasksApi.getById).mockResolvedValueOnce(makeTask())
    const { handle, onReady } = createHandle()
    render(<Probe onReady={onReady} />)

    await waitFor(() => expect(screen.getByTestId('status')).toHaveTextContent('Pending'))

    vi.mocked(tasksApi.updateStatus).mockResolvedValueOnce(makeTask({ status: 'Done' }))
    vi.mocked(tasksApi.getById).mockRejectedValueOnce(networkError())

    await act(async () => {
      await handle.changeStatus?.('Done')
    })

    await waitFor(() => expect(screen.getByTestId('status')).toHaveTextContent('Done'))
    expect(screen.getByTestId('error')).toHaveTextContent('none')
    expect(tasksApi.updateStatus).toHaveBeenCalledWith('task-id', 'Done')
  })

  it('does not let an older in-flight GET overwrite a successful status change', async () => {
    vi.mocked(tasksApi.getById).mockResolvedValueOnce(makeTask())
    const { handle, onReady } = createHandle()
    render(<Probe onReady={onReady} />)

    await waitFor(() => expect(screen.getByTestId('status')).toHaveTextContent('Pending'))

    let releaseOldGet!: () => void
    const gate = new Promise<void>((resolve) => {
      releaseOldGet = resolve
    })

    vi.mocked(tasksApi.getById).mockImplementationOnce(async () => {
      await gate
      return makeTask({ status: 'Pending' })
    })

    await act(async () => {
      void handle.refetch?.(true)
    })
    await waitFor(() => expect(tasksApi.getById).toHaveBeenCalledTimes(2))

    vi.mocked(tasksApi.updateStatus).mockResolvedValueOnce(makeTask({ status: 'Done' }))
    vi.mocked(tasksApi.getById).mockResolvedValueOnce(makeTask({ status: 'Done' }))

    await act(async () => {
      await handle.changeStatus?.('Done')
    })
    await waitFor(() => expect(screen.getByTestId('status')).toHaveTextContent('Done'))

    releaseOldGet()
    await act(async () => {
      await Promise.resolve()
    })

    expect(screen.getByTestId('status')).toHaveTextContent('Done')
  })
})
