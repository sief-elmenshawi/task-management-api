import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react'
import { useLocation, useNavigate } from 'react-router-dom'
import { ApiError } from '../../core/api/ApiError'
import { setUnauthorizedHandler } from '../../core/api/client'
import { endSession, getSessionGeneration, hasSession, startSession } from '../../core/session/session'
import { runLogoutCleanups } from '../../core/session/sessionCleanup'
import { tokenStorage } from '../../core/storage/tokenStorage'
import { authApi } from './api'
import { AuthContext } from './context'
import { clearLoginRedirect, setLoginRedirect } from './loginRedirect'
import type { AuthContextValue, AuthStatus, AuthUser, LoginValues, RegisterValues } from './types'

export function AuthProvider({ children }: { children: ReactNode }) {
  const navigate = useNavigate()
  const location = useLocation()
  const [user, setUser] = useState<AuthUser | null>(null)
  const [status, setStatus] = useState<AuthStatus>(() =>
    hasSession() ? 'loading' : 'unauthenticated',
  )
  const [reloadToken, setReloadToken] = useState(0)

  const clearSession = useCallback(() => {
    endSession()
    setUser(null)
    setStatus('unauthenticated')
  }, [])

  useEffect(() => {
    setUnauthorizedHandler(() => {
      const redirect = { sessionExpired: true }
      setLoginRedirect(redirect)
      navigate('/login', { state: redirect, replace: true })
      clearSession()
    })

    return () => setUnauthorizedHandler(null)
  }, [clearSession, navigate])

  useEffect(() => {
    if (location.pathname === '/login') {
      clearLoginRedirect()
    }
  }, [location.pathname])

  useEffect(() => {
    if (!hasSession()) {
      return
    }

    let cancelled = false
    const startupGeneration = getSessionGeneration()

    authApi
      .me()
      .then((profile) => {
        if (cancelled || getSessionGeneration() !== startupGeneration) {
          return
        }

        setUser({ id: profile.id, name: profile.name, email: profile.email, role: profile.role })
        setStatus('authenticated')
      })
      .catch((error: unknown) => {
        if (cancelled || getSessionGeneration() !== startupGeneration) {
          return
        }

        if (
          error instanceof ApiError &&
          error.status === 401 &&
          !error.temporaryRefreshFailure
        ) {
          clearSession()
          return
        }

        setStatus('error')
      })

    return () => {
      cancelled = true
    }
  }, [clearSession, reloadToken])

  const retrySession = useCallback(() => {
    setStatus(hasSession() ? 'loading' : 'unauthenticated')
    setReloadToken((token) => token + 1)
  }, [])

  const login = useCallback(async (values: LoginValues) => {
    const tokens = await authApi.login(values)
    startSession(tokens)

    const profile = await authApi.me()
    setUser({ id: profile.id, name: profile.name, email: profile.email, role: profile.role })
    setStatus('authenticated')
  }, [])

  const register = useCallback(async (values: RegisterValues) => {
    await authApi.register(values)
  }, [])

  const logout = useCallback(() => {
    const refreshToken = tokenStorage.getRefreshToken()

    void authApi.logout({ refreshToken }).catch(() => undefined)

    runLogoutCleanups(user?.id ?? null)

    setLoginRedirect({})
    navigate('/login', { replace: true })
    clearSession()
  }, [clearSession, navigate, user])

  const value = useMemo<AuthContextValue>(
    () => ({ status, user, login, register, logout, retrySession }),
    [status, user, login, register, logout, retrySession],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}
