import type { ReactNode } from 'react'
import { render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { describe, expect, it, vi } from 'vitest'
import { AuthContext } from '../context'
import type { AuthContextValue, AuthStatus, AuthUser } from '../types'
import { ProtectedRoute } from './ProtectedRoute'
import { RoleRoute } from './RoleRoute'

function authValue(status: AuthStatus, user: AuthUser | null): AuthContextValue {
  return {
    status,
    user,
    login: vi.fn(async () => undefined),
    register: vi.fn(async () => undefined),
    logout: vi.fn(),
  }
}

function renderRoute(value: AuthContextValue, element: ReactNode) {
  return render(
    <AuthContext.Provider value={value}>
      <MemoryRouter initialEntries={['/tasks']}>
        <Routes>
          <Route path="/tasks" element={element} />
          <Route path="/login" element={<div>login page</div>} />
        </Routes>
      </MemoryRouter>
    </AuthContext.Provider>,
  )
}

describe('ProtectedRoute', () => {
  it('shows a spinner while the session is loading', () => {
    renderRoute(
      authValue('loading', null),
      <ProtectedRoute>
        <div>secret</div>
      </ProtectedRoute>,
    )

    expect(screen.getByRole('status')).toBeInTheDocument()
    expect(screen.queryByText('secret')).not.toBeInTheDocument()
  })

  it('redirects to the login page when unauthenticated', () => {
    renderRoute(
      authValue('unauthenticated', null),
      <ProtectedRoute>
        <div>secret</div>
      </ProtectedRoute>,
    )

    expect(screen.getByText('login page')).toBeInTheDocument()
    expect(screen.queryByText('secret')).not.toBeInTheDocument()
  })

  it('renders its children when authenticated', () => {
    renderRoute(
      authValue('authenticated', { name: 'Ada', role: 'User' }),
      <ProtectedRoute>
        <div>secret</div>
      </ProtectedRoute>,
    )

    expect(screen.getByText('secret')).toBeInTheDocument()
  })
})

describe('RoleRoute', () => {
  it('shows access denied for the wrong role', () => {
    renderRoute(
      authValue('authenticated', { name: 'Ada', role: 'User' }),
      <RoleRoute role="Admin">
        <div>admin area</div>
      </RoleRoute>,
    )

    expect(screen.getByText('Access denied')).toBeInTheDocument()
    expect(screen.queryByText('admin area')).not.toBeInTheDocument()
  })

  it('renders its children for the required role', () => {
    renderRoute(
      authValue('authenticated', { name: 'Admin', role: 'Admin' }),
      <RoleRoute role="Admin">
        <div>admin area</div>
      </RoleRoute>,
    )

    expect(screen.getByText('admin area')).toBeInTheDocument()
  })
})