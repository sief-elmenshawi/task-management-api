import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ApiError } from '../../core/api/ApiError'
import { tokenStorage } from '../../core/storage/tokenStorage'
import { AuthProvider } from '../auth/AuthProvider'
import { authApi } from '../auth/api'
import { useAuth } from '../auth/context'
import type { UserResponse } from '../auth/types'
import { saveTaskDraft, taskDraftKey } from './draft'
import { registerTaskDraftLogoutCleanup } from './logoutCleanup'

vi.mock('../auth/api', () => ({
  authApi: {
    me: vi.fn(),
    login: vi.fn(),
    register: vi.fn(),
    logout: vi.fn(),
  },
}))

const profile: UserResponse = {
  id: 'user-1',
  name: 'Ada',
  email: 'ada@example.com',
  role: 'User',
}

function Probe() {
  const { status, logout } = useAuth()

  return (
    <div>
      <span data-testid="status">{status}</span>
      <button type="button" onClick={logout}>
        logout
      </button>
    </div>
  )
}

function renderProvider() {
  return render(
    <MemoryRouter>
      <AuthProvider>
        <Probe />
      </AuthProvider>
    </MemoryRouter>,
  )
}

describe('logout cleanup', () => {
  beforeEach(() => {
    localStorage.clear()
    sessionStorage.clear()
    registerTaskDraftLogoutCleanup()
    vi.mocked(authApi.me).mockReset()
    vi.mocked(authApi.logout).mockReset().mockResolvedValue(undefined)
    tokenStorage.set({ accessToken: 'a', refreshToken: 'r', expiresAt: '2030-01-01T00:00:00Z' })
  })

  it('clears the current user draft on manual logout', async () => {
    vi.mocked(authApi.me).mockResolvedValue(profile)
    saveTaskDraft(profile.id, { title: 'Draft', description: '', priority: 'High' })

    const actor = userEvent.setup()
    renderProvider()

    await waitFor(() => expect(screen.getByTestId('status')).toHaveTextContent('authenticated'))

    await actor.click(screen.getByRole('button', { name: 'logout' }))

    expect(sessionStorage.getItem(taskDraftKey(profile.id))).toBeNull()
  })

  it('clears only the signed-in user draft on manual logout', async () => {
    vi.mocked(authApi.me).mockResolvedValue(profile)
    saveTaskDraft(profile.id, { title: 'Mine', description: '', priority: 'High' })
    saveTaskDraft('other-user', { title: 'Theirs', description: '', priority: 'Low' })

    const actor = userEvent.setup()
    renderProvider()

    await waitFor(() => expect(screen.getByTestId('status')).toHaveTextContent('authenticated'))

    await actor.click(screen.getByRole('button', { name: 'logout' }))

    expect(sessionStorage.getItem(taskDraftKey(profile.id))).toBeNull()
    expect(sessionStorage.getItem(taskDraftKey('other-user'))).not.toBeNull()
  })

  it('keeps the draft when the session expires', async () => {
    vi.mocked(authApi.me).mockRejectedValue(
      new ApiError({ status: 401, code: 'Unauthorized', message: 'Your session has expired.' }),
    )
    saveTaskDraft(profile.id, { title: 'Draft', description: '', priority: 'High' })

    renderProvider()

    await waitFor(() => expect(screen.getByTestId('status')).toHaveTextContent('unauthenticated'))
    expect(sessionStorage.getItem(taskDraftKey(profile.id))).not.toBeNull()
  })
})
