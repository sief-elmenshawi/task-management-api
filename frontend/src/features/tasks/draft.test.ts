import { beforeEach, describe, expect, it } from 'vitest'
import { clearTaskDraft, emptyTaskDraft, loadTaskDraft, saveTaskDraft } from './draft'

describe('task draft', () => {
  beforeEach(() => {
    sessionStorage.clear()
  })

  it('returns an empty draft when nothing is stored', () => {
    expect(loadTaskDraft()).toEqual(emptyTaskDraft)
  })

  it('round-trips a saved draft', () => {
    saveTaskDraft({ title: 'Write report', description: 'Draft body', priority: 'High' })

    expect(loadTaskDraft()).toEqual({
      title: 'Write report',
      description: 'Draft body',
      priority: 'High',
    })
  })

  it('ignores a corrupted draft', () => {
    sessionStorage.setItem('taskmanager.createTaskDraft', '{not json')

    expect(loadTaskDraft()).toEqual(emptyTaskDraft)
  })

  it('drops an unknown priority', () => {
    sessionStorage.setItem(
      'taskmanager.createTaskDraft',
      JSON.stringify({ title: 'x', description: 'y', priority: 'Urgent' }),
    )

    expect(loadTaskDraft()).toEqual({ title: 'x', description: 'y', priority: '' })
  })

  it('clears the draft', () => {
    saveTaskDraft({ title: 'Write report', description: '', priority: 'Low' })
    clearTaskDraft()

    expect(loadTaskDraft()).toEqual(emptyTaskDraft)
  })
})