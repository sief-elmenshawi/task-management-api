import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react'
import { useNavigate } from 'react-router-dom'
import { setUnauthorizedHandler } from '../../core/api/client'
import { tokenStorage } from '../../core/storage/tokenStorage'
import { authApi } from './api'
import { AuthContext } from './context'
import type { AuthContextValue, AuthStatus, AuthUser, LoginValues, RegisterValues } from './types'

export function AuthProvider({ children }: { children: ReactNode }) {
  const navigate = useNavigate()
  const [user, setUser] = useState<AuthUser | null>(null)
  const [status, setStatus] = useState<AuthStatus>(() =>
    tokenStorage.getAccessToken() === null ? 'unauthenticated' : 'loading',
  )

  const clearSession = useCallback(() => {
    tokenStorage.clear()
    setUser(null)
    setStatus('unauthenticated')
  }, [])

  useEffect(() => {
    setUnauthorizedHandler(() => {
      clearSession()
      navigate('/login', { state: { sessionExpired: true }, replace: true })
    })

    return () => setUnauthorizedHandler(null)
  }, [clearSession, navigate])

  useEffect(() => {
    if (tokenStorage.getAccessToken() === null) {
      return
    }

    let cancelled = false

    authApi
      .me()
      .then((profile) => {
        if (cancelled) {
          return
        }

        setUser({ id: profile.id, name: profile.name, email: profile.email, role: profile.role })
        setStatus('authenticated')
      })
      .catch(() => {
        if (cancelled) {
          return
        }

        clearSession()
      })

    return () => {
      cancelled = true
    }
  }, [clearSession])

  const login = useCallback(async (values: LoginValues) => {
    const tokens = await authApi.login(values)
    tokenStorage.set(tokens)

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

    clearSession()
    navigate('/login')
  }, [clearSession, navigate])

  const value = useMemo<AuthContextValue>(
    () => ({ status, user, login, register, logout }),
    [status, user, login, register, logout],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}
