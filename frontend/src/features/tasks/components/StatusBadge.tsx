import { Badge, type BadgeTone } from '../../../shared/components/Badge'
import type { TaskStatus } from '../types'

const toneByStatus: Record<TaskStatus, BadgeTone> = {
  Pending: 'pending',
  InProgress: 'inProgress',
  Done: 'done',
}

const labelByStatus: Record<TaskStatus, string> = {
  Pending: 'Pending',
  InProgress: 'In Progress',
  Done: 'Done',
}

export function StatusBadge({ status }: { status: TaskStatus }) {
  return <Badge tone={toneByStatus[status]}>{labelByStatus[status]}</Badge>
}