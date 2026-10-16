import { useCallback, useEffect, useRef, useState } from 'react'
import { ApiError } from '../../../core/api/ApiError'
import { usersApi } from '../api'
import type { UserSummary } from '../types'

export type UseUsersResult = {
  users: UserSummary[]
  loading: boolean
  error: string | null
  refetch: () => void
  removing: boolean
  removeError: string | null
  remove: (id: string) => Promise<boolean>
}

export function useUsers(): UseUsersResult {
  const [users, setUsers] = useState<UserSummary[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [removing, setRemoving] = useState(false)
  const [removeError, setRemoveError] = useState<string | null>(null)
  const [reloadToken, setReloadToken] = useState(0)
  const removingRef = useRef(false)
  const removeRequestRef = useRef(0)

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

  const remove = useCallback(
    async (id: string): Promise<boolean> => {
      if (removingRef.current) {
        return false
      }

      removingRef.current = true
      const requestId = removeRequestRef.current + 1
      removeRequestRef.current = requestId
      setRemoveError(null)
      setRemoving(true)

      let completed = false

      try {
        await usersApi.remove(id)
        if (removeRequestRef.current === requestId) {
          completed = true
          refetch()
        }
      } catch (requestError) {
        if (removeRequestRef.current === requestId) {
          completed = true
          setRemoveError(
            requestError instanceof ApiError
              ? requestError.message
              : 'Something went wrong. Please try again.',
          )
        }
      } finally {
        if (removeRequestRef.current === requestId) {
          removingRef.current = false
          setRemoving(false)
        }
      }

      return completed
    },
    [refetch],
  )

  return { users, loading, error, refetch, removing, removeError, remove }
}
