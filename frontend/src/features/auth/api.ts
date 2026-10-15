import type { RequestOptions } from '../../core/api/client'
import { api } from '../../core/api/client'
import type { AuthResponse, UserResponse } from './types'

type Options = Omit<RequestOptions, 'method' | 'body'>

export const authApi = {
  login(body: { email: string; password: string }, options: Options = {}): Promise<AuthResponse> {
    return api.post<AuthResponse>('/api/auth/login', body, {
      auth: false,
      skipUnauthorizedHandler: true,
      ...options,
    })
  },

  register(
    body: { name: string; email: string; password: string; confirmPassword: string },
    options: Options = {},
  ): Promise<UserResponse> {
    return api.post<UserResponse>('/api/auth/register', body, {
      auth: false,
      skipUnauthorizedHandler: true,
      ...options,
    })
  },

  logout(body: { refreshToken: string | null }, options: Options = {}): Promise<void> {
    return api.post<void>('/api/auth/logout', body, {
      auth: false,
      skipUnauthorizedHandler: true,
      ...options,
    })
  },

  me(options: Options = {}): Promise<UserResponse> {
    return api.get<UserResponse>('/api/auth/me', options)
  },
}