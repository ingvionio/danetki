import { BrowserRouter, Navigate, Outlet, Route, Routes } from 'react-router-dom'
import { AdminLayout } from './pages/AdminLayout'
import { Login } from './pages/Login'
import { ParserControl } from './pages/ParserControl'
import { PlayPage } from './pages/PlayPage'
import { PuzzlesList } from './pages/PuzzlesList'
import { useAuthStore } from './store/authStore'

function AdminAuthRoute() {
  const token = useAuthStore((s) => s.token)
  const role = useAuthStore((s) => s.role)

  if (!token) {
    return <Navigate to="/login" replace />
  }

  if (role !== 'Admin') {
    return <Navigate to="/" replace />
  }

  return <Outlet />
}

function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/" element={<PlayPage />} />
        <Route path="/login" element={<Login />} />

        <Route element={<AdminAuthRoute />}>
          <Route path="/admin" element={<AdminLayout />}>
            <Route index element={<Navigate to="puzzles" replace />} />
            <Route path="puzzles" element={<PuzzlesList />} />
            <Route path="parser" element={<ParserControl />} />
          </Route>
        </Route>

        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </BrowserRouter>
  )
}

export default App
