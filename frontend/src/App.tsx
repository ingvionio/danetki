import { BrowserRouter, Navigate, Outlet, Route, Routes } from 'react-router-dom'
import { AdminLayout } from './pages/AdminLayout'
import { Login } from './pages/Login'
import { ParserControl } from './pages/ParserControl'
import { PuzzlesList } from './pages/PuzzlesList'
import { useAuthStore } from './store/authStore'

function AuthRoute() {
  const token = useAuthStore((s) => s.token)

  if (!token) {
    return <Navigate to="/login" replace />
  }

  return <Outlet />
}

function AdminRoute() {
  const role = useAuthStore((s) => s.role)

  if (role !== 'Admin') {
    return <Navigate to="/admin/puzzles" replace />
  }

  return <Outlet />
}

function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/login" element={<Login />} />

        <Route element={<AuthRoute />}>
          <Route path="/admin" element={<AdminLayout />}>
            <Route index element={<Navigate to="puzzles" replace />} />
            <Route path="puzzles" element={<PuzzlesList />} />
            <Route element={<AdminRoute />}>
              <Route path="parser" element={<ParserControl />} />
            </Route>
          </Route>
        </Route>

        <Route path="*" element={<Navigate to="/admin/puzzles" replace />} />
      </Routes>
    </BrowserRouter>
  )
}

export default App
