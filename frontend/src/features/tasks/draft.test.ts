import { beforeEach, describe, expect, it } from 'vitest'
import { clearTaskDraft, loadTaskDraft, saveTaskDraft, taskDraftKey } from './draft'

const userId = 'user-1'
const otherUserId = 'user-2'

describe('task draft', () => {
  beforeEach(() => {
    sessionStorage.clear()
  })

  it('returns an empty draft when nothing is saved', () => {
    expect(loadTaskDraft(userId)).toEqual({ title: '', description: '', priority: '' })
  })

  it('restores a saved draft', () => {
    saveTaskDraft(userId, { title: 'Write the report', description: 'Some notes', priority: 'High' })

    expect(loadTaskDraft(userId)).toEqual({
      title: 'Write the report',
      description: 'Some notes',
      priority: 'High',
    })
  })

  it('keeps drafts separate per user', () => {
    saveTaskDraft(userId, { title: 'Mine', description: '', priority: 'High' })

    expect(loadTaskDraft(otherUserId)).toEqual({ title: '', description: '', priority: '' })
    expect(loadTaskDraft(userId)).toEqual({ title: 'Mine', description: '', priority: 'High' })
  })

  it('clears only the given user draft', () => {
    saveTaskDraft(userId, { title: 'Mine', description: '', priority: 'High' })
    saveTaskDraft(otherUserId, { title: 'Theirs', description: '', priority: 'Low' })

    clearTaskDraft(userId)

    expect(loadTaskDraft(userId)).toEqual({ title: '', description: '', priority: '' })
    expect(loadTaskDraft(otherUserId)).toEqual({ title: 'Theirs', description: '', priority: 'Low' })
  })

  it('namespaces the storage key with the user id', () => {
    expect(taskDraftKey(userId)).toBe('taskmanager.createTaskDraft.user-1')
  })
})
