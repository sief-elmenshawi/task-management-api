import { Link } from 'react-router-dom'
import { useDocumentTitle } from '../hooks/useDocumentTitle'
import './pages.css'

export function NotFoundPage() {
  useDocumentTitle('Page not found')

  return (
    <main className="page state-page">
      <h1 className="page__title">Page not found</h1>
      <p className="state-page__message">The page you are looking for does not exist.</p>
      <Link to="/">Go to the home page</Link>
    </main>
  )
}
