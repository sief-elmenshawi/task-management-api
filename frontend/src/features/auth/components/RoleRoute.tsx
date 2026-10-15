import type { ReactNode } from 'react'
import { AccessDeniedPage } from '../../../shared/pages/AccessDeniedPage'
import { useAuth } from '../context'
import type { UserRole } from '../types'

export function RoleRoute({ role, children }: { role: UserRole; children: ReactNode }) {
  const { user } = useAuth()

  if (user?.role !== role) {
    return <AccessDeniedPage />
  }

  return <>{children}</>
}