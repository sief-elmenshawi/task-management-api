import type { ReactNode } from 'react'
import './Badge.css'

export type BadgeTone =
  | 'pending'
  | 'inProgress'
  | 'done'
  | 'high'
  | 'medium'
  | 'low'
  | 'neutral'
  | 'accent'

export type BadgeProps = {
  tone: BadgeTone
  children: ReactNode
}

export function Badge({ tone, children }: BadgeProps) {
  return <span className={`badge badge--${tone}`}>{children}</span>
}
