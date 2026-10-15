import { EmptyState } from '../../../shared/components/EmptyState'

export function UsersPage() {
  return (
    <main className="page">
      <h1 className="page__title">Users</h1>
      <EmptyState title="User management arrives soon" message="Admin user management is built in a later stage." />
    </main>
  )
}