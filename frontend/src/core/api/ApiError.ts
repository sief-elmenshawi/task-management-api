export type ApiErrorInit = {
  status: number
  code: string
  message: string
  fieldErrors?: Record<string, string[]>
  retryAfterSeconds?: number
  temporaryRefreshFailure?: boolean
}

export class ApiError extends Error {
  readonly status: number
  readonly code: string
  readonly fieldErrors: Record<string, string[]>
  readonly retryAfterSeconds?: number
  readonly temporaryRefreshFailure: boolean

  constructor(init: ApiErrorInit) {
    super(init.message)
    this.name = 'ApiError'
    this.status = init.status
    this.code = init.code
    this.fieldErrors = init.fieldErrors ?? {}
    this.retryAfterSeconds = init.retryAfterSeconds
    this.temporaryRefreshFailure = init.temporaryRefreshFailure ?? false
  }
}
