import { describe, expect, it } from 'vitest'
import { countTasks, filterTasks } from './filters'
import type { Task } from './types'

const createdAt = '2026-01-01T10:00:00Z'

const tasks: Task[] = [
  { id: '1', title: 'Write report', description: null, status: 'Pending', priority: 'High', createdAt },
  { id: '2', title: 'Review code', description: null, status: 'InProgress', priority: 'Medium', createdAt },
  { id: '3', title: 'Write tests', status: 'Done', priority: 'Low', description: null, createdAt },
]

describe('filterTasks', () => {
  it('returns every task for the All filter and empty query', () => {
    expect(filterTasks(tasks, 'All', '')).toHaveLength(3)
  })

  it('keeps only tasks with the selected status', () => {
    expect(filterTasks(tasks, 'Done', '').map((task) => task.id)).toEqual(['3'])
  })

  it('matches the title case-insensitively, ignoring surrounding spaces', () => {
    expect(filterTasks(tasks, 'All', '  write  ').map((task) => task.id)).toEqual(['1', '3'])
  })

  it('combines the status filter and search term', () => {
    expect(filterTasks(tasks, 'Done', 'write')).toHaveLength(1)
    expect(filterTasks(tasks, 'Pending', 'write')).toHaveLength(1)
    expect(filterTasks(tasks, 'InProgress', 'write')).toHaveLength(0)
  })
})

describe('countTasks', () => {
  it('counts each status and the total', () => {
    expect(countTasks(tasks)).toEqual({ All: 3, Pending: 1, InProgress: 1, Done: 1 })
  })
})
