import { taskPriorities } from './types'
import type { CreateTaskValues, TaskPriority } from './types'

const draftKeyPrefix = 'taskmanager.createTaskDraft'

export function taskDraftKey(userId: string): string {
  return `${draftKeyPrefix}.${userId}`
}

export const emptyTaskDraft: CreateTaskValues = {
  title: '',
  description: '',
  priority: '',
}

function isPriority(value: unknown): value is TaskPriority | '' {
  return value === '' || taskPriorities.includes(value as TaskPriority)
}

export function loadTaskDraft(userId: string): CreateTaskValues {
  let raw: string | null
  try {
    raw = sessionStorage.getItem(taskDraftKey(userId))
  } catch {
    return { ...emptyTaskDraft }
  }

  if (raw === null) {
    return { ...emptyTaskDraft }
  }

  try {
    const parsed: unknown = JSON.parse(raw)
    if (typeof parsed !== 'object' || parsed === null) {
      return { ...emptyTaskDraft }
    }

    const candidate = parsed as Record<string, unknown>
    return {
      title: typeof candidate.title === 'string' ? candidate.title : '',
      description: typeof candidate.description === 'string' ? candidate.description : '',
      priority: isPriority(candidate.priority) ? candidate.priority : '',
    }
  } catch {
    return { ...emptyTaskDraft }
  }
}

export function saveTaskDraft(userId: string, values: CreateTaskValues): void {
  try {
    sessionStorage.setItem(taskDraftKey(userId), JSON.stringify(values))
  } catch {
    return
  }
}

export function clearTaskDraft(userId: string): void {
  try {
    sessionStorage.removeItem(taskDraftKey(userId))
  } catch {
    return
  }
}