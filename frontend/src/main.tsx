import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import './shared/styles/tokens.css'
import './shared/styles/base.css'
import App from './app/App.tsx'
import { registerTaskDraftLogoutCleanup } from './features/tasks/logoutCleanup'

registerTaskDraftLogoutCleanup()

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <App />
  </StrictMode>,
)
