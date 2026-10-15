import type { RequestOptions } from '../../core/api/client'
import { api } from '../../core/api/client'
import type { CreateUserValues, UserSummary } from './types'

type Options = Omit<RequestOptions, 'method' | 'body'>

export const usersApi = {
  list(options: Options = {}): Promise<UserSummary[]> {
    return api.get<UserSummary[]>('/api/users', options)
  },

  create(body: CreateUserValues, options: Options = {}): Promise<UserSummary> {
    return api.post<UserSummary>('/api/users', body, options)
  },

  remove(id: string, options: Options = {}): Promise<void> {
    return api.delete<void>(`/api/users/${id}`, options)
  },
}