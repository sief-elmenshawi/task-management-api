import { createBrowserRouter } from 'react-router-dom'
import { NotFoundPage } from '../shared/pages/NotFoundPage'
import { PlaceholderPage } from './PlaceholderPage'

export const router = createBrowserRouter([
  { path: '/', element: <PlaceholderPage /> },
  { path: '*', element: <NotFoundPage /> },
])
