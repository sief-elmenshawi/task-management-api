import { EmptyState } from '../../../shared/components/EmptyState'

export function TasksPage() {
  return (
    <main className="page">
      <h1 className="page__title">Tasks</h1>
      <EmptyState
        title="Task management arrives next"
        message="Listing, creating and updating tasks is built in the next stage."
      />
    </main>
  )
}