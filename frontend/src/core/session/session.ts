import { tokenStorage, type TokenPair } from '../storage/tokenStorage'

type SessionInvalidationListener = () => void

let generation = 0
const invalidationListeners = new Set<SessionInvalidationListener>()

export function onSessionInvalidated(listener: SessionInvalidationListener): () => void {
  invalidationListeners.add(listener)

  return () => {
    invalidationListeners.delete(listener)
  }
}

export function getSessionGeneration(): number {
  return generation
}

export function invalidateSession(): void {
  generation += 1

  for (const listener of invalidationListeners) {
    listener()
  }
}

export function hasSession(): boolean {
  return tokenStorage.getAccessToken() !== null
}

export function startSession(tokens: TokenPair): void {
  invalidateSession()
  tokenStorage.set(tokens)
}

export function endSession(): void {
  invalidateSession()
  tokenStorage.clear()
}
