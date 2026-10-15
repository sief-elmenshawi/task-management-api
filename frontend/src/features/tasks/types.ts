export type TaskStatus = 'Pending' | 'InProgress' | 'Done'

export type TaskPriority = 'Low' | 'Medium' | 'High'

export type Task = {
  id: string
  title: string
  description: string | null
  status: TaskStatus
  priority: TaskPriority
  createdAt: string
}

export type CreateTaskRequest = {
  title: string
  description: string | null
  priority: TaskPriority
}

export type CreateTaskValues = {
  title: string
  description: string
  priority: TaskPriority | ''
}

export const taskStatuses: readonly TaskStatus[] = ['Pending', 'InProgress', 'Done']

export const taskPriorities: readonly TaskPriority[] = ['Low', 'Medium', 'High']

export const titleMaxLength = 200

export const descriptionMaxLength = 1000