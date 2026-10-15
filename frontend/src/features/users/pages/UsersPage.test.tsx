import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { AuthContext } from '../../auth/context'
import type { AuthContextValue } from '../../auth/types'
import { usersApi } from '../api'
import type { UserSummary } from '../types'
import { UsersPage } from './UsersPage'

vi.mock('../api', () => ({
  usersApi: { list: vi.fn(), remove: vi.fn() },
}))

const people: UserSummary[] = [
  {
    id: '1',
    name: 'Ada',
    email: 'ada@example.com',
    role: 'User',
    createdAt: '2026-01-01T10:00:00Z',
  },
  {
    id: '2',
    name: 'Admin',
    email: 'admin@example.com',
    role: 'Admin',
    createdAt: '2026-01-02T10:00:00Z',
  },
]

function renderPage() {
  const authValue: AuthContextValue = {
    status: 'authenticated',
    user: { id: '2', name: 'Admin', email: 'admin@example.com', role: 'Admin' },
    login: vi.fn(async () => undefined),
    register: vi.fn(async () => undefined),
    logout: vi.fn(),
    retrySession: vi.fn(),
  }

  return render(
    <AuthContext.Provider value={authValue}>
      <MemoryRouter>
        <UsersPage />
      </MemoryRouter>
    </AuthContext.Provider>,
  )
}

describe('UsersPage', () => {
  beforeEach(() => {
    vi.mocked(usersApi.list).mockReset().mockResolvedValue(people)
    vi.mocked(usersApi.remove).mockReset().mockResolvedValue(undefined)
  })

  it('deletes a user after confirmation', async () => {
    const user = userEvent.setup()
    renderPage()

    const deleteButtons = await screen.findAllByRole('button', { name: 'Delete' })
    await user.click(deleteButtons[0])

    const dialog = screen.getByRole('dialog')
    expect(within(dialog).getByText(/Ada/)).toBeInTheDocument()

    await user.click(within(dialog).getByRole('button', { name: 'Delete' }))

    expect(usersApi.remove).toHaveBeenCalledWith('1')
  })

  it('does not delete when the dialog is cancelled', async () => {
    const user = userEvent.setup()
    renderPage()

    const deleteButtons = await screen.findAllByRole('button', { name: 'Delete' })
    await user.click(deleteButtons[0])

    const dialog = screen.getByRole('dialog')
    await user.click(within(dialog).getByRole('button', { name: 'Cancel' }))

    expect(usersApi.remove).not.toHaveBeenCalled()
  })

  it('prevents deleting your own account', async () => {
    renderPage()

    const deleteButtons = await screen.findAllByRole('button', { name: 'Delete' })
    expect(deleteButtons[0]).toBeEnabled()
    expect(deleteButtons[1]).toBeDisabled()
  })
})
