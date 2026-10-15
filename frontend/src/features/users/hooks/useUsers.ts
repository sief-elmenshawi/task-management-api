import { useCallback, useEffect, useState } from 'react'
import { usersApi } from '../api'
import type { UserSummary } from '../types'

export type UseUsersResult = {
  users: UserSummary[]
  loading: boolean
  error: string | null
  refetch: () => void
}

export function useUsers(): UseUsersResult {
  const [users, setUsers] = useState<UserSummary[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [reloadToken, setReloadToken] = useState(0)

  useEffect(() => {
    const controller = new AbortController()
    let cancelled = false

    usersApi
      .list({ signal: controller.signal })
      .then((data) => {
        if (cancelled) {
          return
        }

        setUsers(data)
        setError(null)
        setLoading(false)
      })
      .catch((requestError: unknown) => {
        if (cancelled || controller.signal.aborted) {
          return
        }

        setError(requestError instanceof Error ? requestError.message : 'Failed to load users.')
        setLoading(false)
      })

    return () => {
      cancelled = true
      controller.abort()
    }
  }, [reloadToken])

  const refetch = useCallback(() => {
    setLoading(true)
    setReloadToken((token) => token + 1)
  }, [])

  return { users, loading, error, refetch }
}