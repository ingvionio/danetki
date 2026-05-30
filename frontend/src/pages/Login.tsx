import { type FormEvent, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { login } from '../api/services'
import { Button } from '../components/ui/Button'
import { Card } from '../components/ui/Card'
import { Input } from '../components/ui/Input'
import { decodeJwtRole } from '../lib/utils'
import { useAuthStore } from '../store/authStore'

export function Login() {
  const navigate = useNavigate()
  const setAuth = useAuthStore((s) => s.setAuth)

  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [isLoading, setIsLoading] = useState(false)

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    setError(null)
    setIsLoading(true)

    try {
      const response = await login(email, password)
      const role = decodeJwtRole(response.token)
      setAuth(response.token, role)
      navigate(role === 'Admin' ? '/admin/puzzles' : '/dashboard')
    } catch {
      setError('Неверный email или пароль')
    } finally {
      setIsLoading(false)
    }
  }

  return (
    <div className="flex min-h-screen items-center justify-center bg-zinc-950 px-4">
      <div className="w-full max-w-md">
        <div className="mb-8 text-center">
          <h1 className="text-2xl font-semibold tracking-tight text-white">Danetka</h1>
          <p className="mt-2 text-sm text-zinc-500">Вход в личный кабинет</p>
        </div>

        <Card>
          <form onSubmit={handleSubmit} className="space-y-4">
            <div className="space-y-2">
              <label htmlFor="email" className="text-sm font-medium text-zinc-300">
                Email
              </label>
              <Input
                id="email"
                type="email"
                autoComplete="email"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                placeholder="admin@danetka.local"
                required
              />
            </div>

            <div className="space-y-2">
              <label htmlFor="password" className="text-sm font-medium text-zinc-300">
                Пароль
              </label>
              <Input
                id="password"
                type="password"
                autoComplete="current-password"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                placeholder="••••••••"
                required
              />
            </div>

            {error && (
              <p className="text-sm text-red-400">{error}</p>
            )}

            <Button type="submit" className="w-full" isLoading={isLoading}>
              Войти
            </Button>
          </form>
        </Card>
      </div>
    </div>
  )
}
