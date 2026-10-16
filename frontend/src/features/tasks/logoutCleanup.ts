import { registerLogoutCleanup } from '../../core/session/sessionCleanup'
import { clearTaskDraft } from './draft'

export function registerTaskDraftLogoutCleanup(): void {
  registerLogoutCleanup((userId) => {
    if (userId !== null) {
      clearTaskDraft(userId)
    }
  })
}
