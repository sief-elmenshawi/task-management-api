import { act, renderHook } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { usePolling } from './usePolling'

function setHidden(hidden: boolean) {
  Object.defineProperty(document, 'hidden', { configurable: true, get: () => hidden })
}

afterEach(() => {
  vi.useRealTimers()
  delete (document as { hidden?: unknown }).hidden
})

describe('usePolling', () => {
  it('runs the callback once per interval', async () => {
    vi.useFakeTimers()
    const callback = vi.fn().mockResolvedValue(undefined)

    renderHook(() => usePolling(callback, 1000, true))

    await vi.advanceTimersByTimeAsync(1000)
    expect(callback).toHaveBeenCalledTimes(1)

    await vi.advanceTimersByTimeAsync(1000)
    expect(callback).toHaveBeenCalledTimes(2)
  })

  it('does nothing while disabled', async () => {
    vi.useFakeTimers()
    const callback = vi.fn().mockResolvedValue(undefined)

    renderHook(() => usePolling(callback, 1000, false))

    await vi.advanceTimersByTimeAsync(5000)
    expect(callback).not.toHaveBeenCalled()
  })

  it('pauses while the document is hidden and resumes when visible', async () => {
    vi.useFakeTimers()
    const callback = vi.fn().mockResolvedValue(undefined)

    setHidden(true)
    renderHook(() => usePolling(callback, 1000, true))

    await vi.advanceTimersByTimeAsync(3000)
    expect(callback).not.toHaveBeenCalled()

    setHidden(false)
    act(() => {
      document.dispatchEvent(new Event('visibilitychange'))
    })

    await vi.advanceTimersByTimeAsync(0)
    expect(callback).toHaveBeenCalledTimes(1)
  })

  it('stops running after unmount', async () => {
    vi.useFakeTimers()
    const callback = vi.fn().mockResolvedValue(undefined)

    const { unmount } = renderHook(() => usePolling(callback, 1000, true))
    unmount()

    await vi.advanceTimersByTimeAsync(5000)
    expect(callback).not.toHaveBeenCalled()
  })
})