import { Badge, type BadgeTone } from '../../../shared/components/Badge'
import type { TaskPriority } from '../types'

const toneByPriority: Record<TaskPriority, BadgeTone> = {
  Low: 'low',
  Medium: 'medium',
  High: 'high',
}

export function PriorityBadge({ priority }: { priority: TaskPriority }) {
  return <Badge tone={toneByPriority[priority]}>{priority}</Badge>
}