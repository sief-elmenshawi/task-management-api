import { Alert } from '../shared/components/Alert'
import { Badge } from '../shared/components/Badge'
import { Button } from '../shared/components/Button'
import './PlaceholderPage.css'

export function PlaceholderPage() {
  return (
    <>
      <header className="app-header">
        <div className="app-header__inner">
          <strong>Task Management</strong>
        </div>
      </header>
      <main className="page">
        <h1 className="page__title">Frontend foundation</h1>
        <Alert variant="info">
          Design tokens, the API client and the shared components are ready. Tasks, users and auth
          screens arrive in the next stage.
        </Alert>
        <div className="placeholder__badges">
          <Badge tone="pending">Pending</Badge>
          <Badge tone="inProgress">In Progress</Badge>
          <Badge tone="done">Done</Badge>
          <Badge tone="high">High</Badge>
          <Badge tone="medium">Medium</Badge>
          <Badge tone="low">Low</Badge>
        </div>
        <Button>Primary button</Button>
      </main>
    </>
  )
}
