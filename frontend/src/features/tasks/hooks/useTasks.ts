import { useCallback, useEffect, useRef, useState } from 'react'
import { ApiError } from '../../../core/api/ApiError'
import { tasksApi } from '../api'
import type { Task } from '../types'

export type UseTasksResult = {
  tasks: Task[]
  loading: boolean
  error: string | null
  refetch: (silent?: boolean) => Promise<void>
}

function messageOf(error: unknown): string {
  return error instanceof ApiError ? error.message : 'Something went wrong. Please try again.'
}

export function useTasks(): UseTasksResult {
  const [tasks, setTasks] = useState<Task[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
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
      .list({ signal: controller.signal })
      .then((data) => {
        if (cancelled) {
          return
        }

        setTasks(data)
        setError(null)
        setLoading(false)
        settle()
      })
      .catch((requestError: unknown) => {
        if (cancelled || controller.signal.aborted) {
          return
        }

        setError(messageOf(requestError))
        setLoading(false)
        settle()
      })

    return () => {
      cancelled = true
      controller.abort()
    }
  }, [reloadToken])

  const refetch = useCallback((silent = false) => {
    silentRef.current = silent
    return new Promise<void>((resolve) => {
      resolversRef.current.push(resolve)
      setReloadToken((token) => token + 1)
    })
  }, [])

  return { tasks, loading, error, refetch }
}