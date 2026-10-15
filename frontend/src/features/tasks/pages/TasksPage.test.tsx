import { act, render } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { tasksApi } from '../api'
import type { Task } from '../types'
import { TasksPage } from './TasksPage'

vi.mock('../api', () => ({
  tasksApi: { list: vi.fn(), updateStatus: vi.fn() },
}))

function makeTask(overrides: Partial<Task>): Task {
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

function renderPage() {
  return render(
    <MemoryRouter>
      <TasksPage />
    </MemoryRouter>,
  )
}

describe('TasksPage polling', () => {
  beforeEach(() => {
    vi.mocked(tasksApi.list).mockReset()
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  it('keeps polling while any task in the list is pending, not only the newest', async () => {
    vi.useFakeTimers()
    vi.mocked(tasksApi.list).mockResolvedValue([
      makeTask({ id: 'done', title: 'Finished first', status: 'Done' }),
      makeTask({ id: 'backlog', title: 'Old backlog item', status: 'Pending' }),
    ])

    renderPage()
    await act(async () => {
      await vi.advanceTimersByTimeAsync(0)
    })
    expect(tasksApi.list).toHaveBeenCalledTimes(1)

    await act(async () => {
      await vi.advanceTimersByTimeAsync(2000)
    })
    expect(tasksApi.list).toHaveBeenCalledTimes(2)
  })

  it('stops polling once no task is pending', async () => {
    vi.useFakeTimers()
    vi.mocked(tasksApi.list).mockResolvedValue([
      makeTask({ id: 'done', title: 'Finished first', status: 'Done' }),
      makeTask({ id: 'in-progress', title: 'Working', status: 'InProgress' }),
    ])

    renderPage()
    await act(async () => {
      await vi.advanceTimersByTimeAsync(0)
    })
    expect(tasksApi.list).toHaveBeenCalledTimes(1)

    await act(async () => {
      await vi.advanceTimersByTimeAsync(6000)
    })
    expect(tasksApi.list).toHaveBeenCalledTimes(1)
  })
})
