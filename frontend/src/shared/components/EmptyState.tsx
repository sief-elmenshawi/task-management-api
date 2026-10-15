import './EmptyState.css'

export type EmptyStateProps = {
  title: string
  message?: string
}

export function EmptyState({ title, message }: EmptyStateProps) {
  return (
    <div className="empty-state">
      <p className="empty-state__title">{title}</p>
      {message && <p className="empty-state__message">{message}</p>}
    </div>
  )
}
