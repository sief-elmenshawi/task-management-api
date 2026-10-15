import { AuthProvider } from '../features/auth/AuthProvider'
import { Layout } from './Layout'

export function RootLayout() {
  return (
    <AuthProvider>
      <Layout />
    </AuthProvider>
  )
}