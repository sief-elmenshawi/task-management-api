import { useEffect, useRef } from 'react'

export function usePolling(
  callback: () => void | Promise<void>,
  intervalMs: number,
  enabled: boolean,
): void {
  const callbackRef = useRef(callback)

  useEffect(() => {
    callbackRef.current = callback
  }, [callback])

  useEffect(() => {
    if (!enabled) {
      return
    }

    let cancelled = false
    let paused = false
    let timer: number | undefined

    async function tick() {
      timer = undefined

      if (cancelled || document.hidden) {
        paused = document.hidden
        return
      }

      await callbackRef.current()

      if (cancelled) {
        return
      }

      timer = window.setTimeout(tick, intervalMs)
    }

    function onVisibilityChange() {
      if (cancelled || document.hidden || !paused) {
        return
      }

      paused = false
      timer = window.setTimeout(tick, 0)
    }

    document.addEventListener('visibilitychange', onVisibilityChange)
    timer = window.setTimeout(tick, intervalMs)

    return () => {
      cancelled = true

      if (timer !== undefined) {
        clearTimeout(timer)
      }

      document.removeEventListener('visibilitychange', onVisibilityChange)
    }
  }, [enabled, intervalMs])
}