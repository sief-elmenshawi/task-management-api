import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ApiError } from '../../../core/api/ApiError'
import { tasksApi } from '../api'
import { CreateTaskPage } from './CreateTaskPage'

vi.mock('../api', () => ({
  tasksApi: { create: vi.fn() },
}))

function renderPage() {
  return render(
    <MemoryRouter>
      <CreateTaskPage />
    </MemoryRouter>,
  )
}

describe('CreateTaskPage', () => {
  beforeEach(() => {
    vi.mocked(tasksApi.create).mockReset()
  })

  it('shows the API duplicate message when the title already exists today', async () => {
    vi.mocked(tasksApi.create).mockRejectedValue(
      new ApiError({
        status: 409,
        code: 'TaskTitleDuplicate',
        message: 'You already created a task with this title today.',
      }),
    )

    const user = userEvent.setup()
    renderPage()

    await user.type(screen.getByLabelText('Title'), 'Write the report')
    await user.selectOptions(screen.getByLabelText('Priority'), 'High')
    await user.click(screen.getByRole('button', { name: 'Create task' }))

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'You already created a task with this title today.',
    )
    expect(screen.getByLabelText('Title')).toHaveValue('Write the report')
  })

  it('validates before sending the request', async () => {
    const user = userEvent.setup()
    renderPage()

    await user.click(screen.getByRole('button', { name: 'Create task' }))

    expect(screen.getByText('Title is required.')).toBeInTheDocument()
    expect(screen.getByText('Priority is required.')).toBeInTheDocument()
    expect(tasksApi.create).not.toHaveBeenCalled()
  })
})