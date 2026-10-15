import { useCallback, useEffect, useRef, useState } from 'react'
import { ApiError } from '../../../core/api/ApiError'
import { tasksApi } from '../api'
import type { Task } from '../types'

export type UseTaskResult = {
  task: Task | null
  loading: boolean
  error: string | null
  notFound: boolean
  refetch: (silent?: boolean) => Promise<void>
}

function messageOf(error: unknown): string {
  return error instanceof ApiError ? error.message : 'Something went wrong. Please try again.'
}

export function useTask(id: string): UseTaskResult {
  const [task, setTask] = useState<Task | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [notFound, setNotFound] = useState(false)
  const [reloadToken, setReloadToken] = useState(0)
  const silentRef = useRef(false)
  const resolversRef = useRef<Array<() => void>>([])

  useEffect(() => {
    const controller = new AbortController()
    let cancelled = false
    const silent = silentRef.current
    silentRef.current = false

    function settle() {
      const resolvers = resolversRef.current
      resolversRef.current = []
      resolvers.forEach((resolve) => resolve())
    }

    if (!silent) {
      setLoading(true)
    }

    tasksApi
      .getById(id, { signal: controller.signal })
      .then((data) => {
        if (cancelled) {
          return
        }

        setTask(data)
        setError(null)
        setNotFound(false)
        setLoading(false)
        settle()
      })
      .catch((requestError: unknown) => {
        if (cancelled || controller.signal.aborted) {
          return
        }

        if (!silent) {
          if (requestError instanceof ApiError && requestError.status === 404) {
            setNotFound(true)
            setError(null)
          } else {
            setError(messageOf(requestError))
          }

          setLoading(false)
        }

        settle()
      })

    return () => {
      cancelled = true
      controller.abort()
    }
  }, [id, reloadToken])

  const refetch = useCallback((silent = false) => {
    silentRef.current = silent
    return new Promise<void>((resolve) => {
      resolversRef.current.push(resolve)
      setReloadToken((token) => token + 1)
    })
  }, [])

  return { task, loading, error, notFound, refetch }
}