import { BrowserRouter, Navigate, Outlet, Route, Routes } from 'react-router-dom'
import { AdminLayout } from './pages/AdminLayout'
import { AdminUsers } from './pages/AdminUsers'
import { Login } from './pages/Login'
import { ParserControl } from './pages/ParserControl'
import { PuzzlesList } from './pages/PuzzlesList'
import { UserDashboard } from './pages/UserDashboard'
import { useAuthStore } from './store/authStore'

function LoginRoute() {
  const token = useAuthStore((s) => s.token)
  const role = useAuthStore((s) => s.role)

  if (token) {
    return <Navigate to={role === 'Admin' ? '/admin/puzzles' : '/dashboard'} replace />
  }

  return <Login />
}

function UserRoute() {
  const token = useAuthStore((s) => s.token)
  const role = useAuthStore((s) => s.role)

  if (!token) {
    return <Navigate to="/login" replace />
  }

  if (role === 'Admin') {
    return <Navigate to="/admin/puzzles" replace />
  }

  return <Outlet />
}

function AdminAuthRoute() {
  const token = useAuthStore((s) => s.token)
  const role = useAuthStore((s) => s.role)

  if (!token) {
    return <Navigate to="/login" replace />
  }

  if (role !== 'Admin') {
    return <Navigate to="/dashboard" replace />
  }

  return <Outlet />
}

function RootRedirect() {
  const token = useAuthStore((s) => s.token)
  const role = useAuthStore((s) => s.role)

  if (!token) {
    return <Navigate to="/login" replace />
  }

  return <Navigate to={role === 'Admin' ? '/admin/puzzles' : '/dashboard'} replace />
}

function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/login" element={<LoginRoute />} />
        <Route path="/" element={<RootRedirect />} />

        <Route element={<UserRoute />}>
          <Route path="/dashboard" element={<UserDashboard />} />
        </Route>

        <Route element={<AdminAuthRoute />}>
          <Route path="/admin" element={<AdminLayout />}>
            <Route index element={<Navigate to="puzzles" replace />} />
            <Route path="puzzles" element={<PuzzlesList />} />
            <Route path="parser" element={<ParserControl />} />
            <Route path="users" element={<AdminUsers />} />
          </Route>
        </Route>

        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </BrowserRouter>
  )
}

export default App
