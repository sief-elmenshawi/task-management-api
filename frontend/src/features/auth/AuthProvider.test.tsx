import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ApiError } from '../../core/api/ApiError'
import { tokenStorage } from '../../core/storage/tokenStorage'
import { saveTaskDraft, taskDraftKey } from '../tasks/draft'
import { AuthProvider } from './AuthProvider'
import { authApi } from './api'
import { useAuth } from './context'
import type { UserResponse } from './types'

vi.mock('./api', () => ({
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
  const { status, logout, retrySession } = useAuth()

  return (
    <div>
      <span data-testid="status">{status}</span>
      <button type="button" onClick={logout}>
        logout
      </button>
      <button type="button" onClick={retrySession}>
        retry
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

describe('AuthProvider', () => {
  beforeEach(() => {
    localStorage.clear()
    sessionStorage.clear()
    vi.mocked(authApi.me).mockReset()
    vi.mocked(authApi.logout).mockReset().mockResolvedValue(undefined)
    tokenStorage.set({ accessToken: 'a', refreshToken: 'r', expiresAt: '2030-01-01T00:00:00Z' })
  })

  it('keeps the session and exposes an error state when the startup check is unreachable', async () => {
    vi.mocked(authApi.me).mockRejectedValue(
      new ApiError({ status: 0, code: 'NetworkError', message: 'Cannot reach the server' }),
    )

    renderProvider()

    await waitFor(() => expect(screen.getByTestId('status')).toHaveTextContent('error'))
    expect(tokenStorage.getAccessToken()).toBe('a')
    expect(tokenStorage.getRefreshToken()).toBe('r')
  })

  it('clears the session when the startup check is rejected with 401', async () => {
    vi.mocked(authApi.me).mockRejectedValue(
      new ApiError({ status: 401, code: 'Unauthorized', message: 'Authentication is required.' }),
    )

    renderProvider()

    await waitFor(() => expect(screen.getByTestId('status')).toHaveTextContent('unauthenticated'))
    expect(tokenStorage.getAccessToken()).toBeNull()
  })

  it('retries the startup check and authenticates once the server recovers', async () => {
    vi.mocked(authApi.me)
      .mockRejectedValueOnce(
        new ApiError({ status: 0, code: 'NetworkError', message: 'Cannot reach the server' }),
      )
      .mockResolvedValueOnce(profile)

    const actor = userEvent.setup()
    renderProvider()

    await waitFor(() => expect(screen.getByTestId('status')).toHaveTextContent('error'))

    await actor.click(screen.getByRole('button', { name: 'retry' }))

    await waitFor(() => expect(screen.getByTestId('status')).toHaveTextContent('authenticated'))
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
