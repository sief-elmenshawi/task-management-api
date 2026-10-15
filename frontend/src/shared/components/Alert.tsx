import type { ReactNode } from 'react'
import './Alert.css'

export type AlertProps = {
  variant: 'error' | 'success' | 'info'
  children: ReactNode
}

export function Alert({ variant, children }: AlertProps) {
  return (
    <div className={`alert alert--${variant}`} role={variant === 'error' ? 'alert' : 'status'}>
      {children}
    </div>
  )
}
