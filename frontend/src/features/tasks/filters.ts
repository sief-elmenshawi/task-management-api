import type { Task, TaskStatus } from './types'

export type TaskFilter = 'All' | TaskStatus

export const taskFilters: readonly TaskFilter[] = ['All', 'Pending', 'InProgress', 'Done']

export function filterTasks(tasks: Task[], filter: TaskFilter, query: string): Task[] {
  const term = query.trim().toLowerCase()

  return tasks.filter((task) => {
    if (filter !== 'All' && task.status !== filter) {
      return false
    }

    if (term.length > 0 && !task.title.toLowerCase().includes(term)) {
      return false
    }

    return true
  })
}

export function countTasks(tasks: Task[]): Record<TaskFilter, number> {
  return {
    All: tasks.length,
    Pending: tasks.filter((task) => task.status === 'Pending').length,
    InProgress: tasks.filter((task) => task.status === 'InProgress').length,
    Done: tasks.filter((task) => task.status === 'Done').length,
  }
}