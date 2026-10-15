import { Badge } from '../../../shared/components/Badge'
import type { UserRole } from '../../auth/types'

export function RoleBadge({ role }: { role: UserRole }) {
  return <Badge tone={role === 'Admin' ? 'accent' : 'neutral'}>{role}</Badge>
}