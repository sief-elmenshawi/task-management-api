import { Link } from 'react-router-dom'
import './pages.css'

export function AccessDeniedPage() {
  return (
    <main className="page state-page">
      <h1 className="page__title">Access denied</h1>
      <p className="state-page__message">You do not have permission to view this page.</p>
      <Link to="/">Go to the home page</Link>
    </main>
  )
}
