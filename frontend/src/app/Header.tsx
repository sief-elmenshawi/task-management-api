import { NavLink } from 'react-router-dom'
import { useAuth } from '../features/auth/context'
import { Badge } from '../shared/components/Badge'
import { Button } from '../shared/components/Button'
import './Header.css'

function linkClass({ isActive }: { isActive: boolean }): string {
  return isActive ? 'app-header__link app-header__link--active' : 'app-header__link'
}

export function Header() {
  const { status, user, logout } = useAuth()

  return (
    <header className="app-header">
      <div className="app-header__inner app-header__bar">
        <NavLink to="/tasks" className="app-header__brand">
          Task Management
        </NavLink>

        {status === 'authenticated' && (
          <nav className="app-header__nav">
            <NavLink to="/tasks" className={linkClass}>
              Tasks
            </NavLink>
            {user?.role === 'Admin' && (
              <NavLink to="/admin/users" className={linkClass}>
                Users
              </NavLink>
            )}
          </nav>
        )}

        {user && (
          <div className="app-header__user">
            <span className="app-header__user-name">{user.name}</span>
            <Badge tone={user.role === 'Admin' ? 'accent' : 'neutral'}>{user.role}</Badge>
            <Button variant="secondary" onClick={logout}>
              Log out
            </Button>
          </div>
        )}
      </div>
    </header>
  )
}