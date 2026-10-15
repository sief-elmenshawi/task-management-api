import type { RequestOptions } from '../../core/api/client'
import { api } from '../../core/api/client'
import type { CreateTaskRequest, Task, TaskStatus } from './types'

type Options = Omit<RequestOptions, 'method' | 'body'>

export const tasksApi = {
  list(options: Options = {}): Promise<Task[]> {
    return api.get<Task[]>('/api/tasks', options)
  },

  getById(id: string, options: Options = {}): Promise<Task> {
    return api.get<Task>(`/api/tasks/${id}`, options)
  },

  create(body: CreateTaskRequest, options: Options = {}): Promise<Task> {
    return api.post<Task>('/api/tasks', body, options)
  },

  updateStatus(id: string, status: TaskStatus, options: Options = {}): Promise<Task> {
    return api.patch<Task>(`/api/tasks/${id}/status`, { status }, options)
  },
}