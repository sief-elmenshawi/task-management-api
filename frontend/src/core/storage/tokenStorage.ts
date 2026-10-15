const accessTokenKey = 'taskmanager.accessToken'
const refreshTokenKey = 'taskmanager.refreshToken'
const expiresAtKey = 'taskmanager.expiresAt'

export type TokenPair = {
  accessToken: string
  refreshToken: string
  expiresAt: string
}

export const tokenStorage = {
  getAccessToken(): string | null {
    return localStorage.getItem(accessTokenKey)
  },

  getRefreshToken(): string | null {
    return localStorage.getItem(refreshTokenKey)
  },

  getExpiresAt(): string | null {
    return localStorage.getItem(expiresAtKey)
  },

  set({ accessToken, refreshToken, expiresAt }: TokenPair): void {
    localStorage.setItem(accessTokenKey, accessToken)
    localStorage.setItem(refreshTokenKey, refreshToken)
    localStorage.setItem(expiresAtKey, expiresAt)
  },

  clear(): void {
    localStorage.removeItem(accessTokenKey)
    localStorage.removeItem(refreshTokenKey)
    localStorage.removeItem(expiresAtKey)
  },
}
