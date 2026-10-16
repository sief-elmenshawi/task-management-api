type LogoutCleanup = (userId: string | null) => void

const cleanups = new Set<LogoutCleanup>()

export function registerLogoutCleanup(cleanup: LogoutCleanup): void {
  cleanups.add(cleanup)
}

export function runLogoutCleanups(userId: string | null = null): void {
  for (const cleanup of cleanups) {
    cleanup(userId)
  }
}
