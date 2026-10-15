import type { ApiRequestOptions } from '@/core/api/client'
import { api } from '@/core/api/client'
import type { AuthResponse, UserResponse } from './types'

export const authApi = {
  login: (body: { email: string; password: string }, options?: Partial<ApiRequestOptions>) =>
    api.post<AuthResponse>('/api/auth/login', body, { auth: false, skipUnauthorizedHandler: true, ...options }),
  register: (body: { name: string; email: string; password: string; confirmPassword: string }, options?: Partial<ApiRequestOptions>) =>
    api.post<AuthResponse>('/api/auth/register', body, { auth: false, skipUnauthorizedHandler: true, ...options }),
  logout: (body: { refreshToken: string | null }, options?: Partial<ApiRequestOptions>) =>
    api.post<void>('/api/auth/logout', body, { auth: false, skipUnauthorizedHandler: true, ...options }),
  me: (options?: Partial<ApiRequestOptions>) =>
    api.get<UserResponse>('/api/auth/me', options),
}