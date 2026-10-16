export type LoginRedirectState = {
  sessionExpired?: boolean
}

let pending: LoginRedirectState | null = null

export function setLoginRedirect(state: LoginRedirectState): void {
  pending = state
}

export function getLoginRedirect(): LoginRedirectState | null {
  return pending
}

export function clearLoginRedirect(): void {
  pending = null
}
