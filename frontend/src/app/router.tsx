import { createBrowserRouter, Navigate, Outlet } from 'react-router-dom'
import { ProtectedRoute } from '../features/auth/components/ProtectedRoute'
import { RoleRoute } from '../features/auth/components/RoleRoute'
import { LoginPage } from '../features/auth/pages/LoginPage'
import { RegisterPage } from '../features/auth/pages/RegisterPage'
import { CreateTaskPage } from '../features/tasks/pages/CreateTaskPage'
import { TaskDetailsPage } from '../features/tasks/pages/TaskDetailsPage'
import { TasksPage } from '../features/tasks/pages/TasksPage'
import { CreateUserPage } from '../features/users/pages/CreateUserPage'
import { UsersPage } from '../features/users/pages/UsersPage'
import { NotFoundPage } from '../shared/pages/NotFoundPage'
import { RootLayout } from './RootLayout'

export const router = createBrowserRouter([
  {
    path: '/',
    element: <RootLayout />,
    children: [
      { index: true, element: <Navigate to="/tasks" replace /> },
      { path: 'login', element: <LoginPage /> },
      { path: 'register', element: <RegisterPage /> },
      {
        element: (
          <ProtectedRoute>
            <Outlet />
          </ProtectedRoute>
        ),
        children: [
          { path: 'tasks', element: <TasksPage /> },
          { path: 'tasks/new', element: <CreateTaskPage /> },
          { path: 'tasks/:id', element: <TaskDetailsPage /> },
        ],
      },
      {
        path: 'admin',
        element: (
          <ProtectedRoute>
            <RoleRoute role="Admin">
              <Outlet />
            </RoleRoute>
          </ProtectedRoute>
        ),
        children: [
          { path: 'users', element: <UsersPage /> },
          { path: 'users/new', element: <CreateUserPage /> },
        ],
      },
      { path: '*', element: <NotFoundPage /> },
    ],
  },
])