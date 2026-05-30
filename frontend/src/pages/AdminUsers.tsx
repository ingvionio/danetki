import { Coins, RefreshCw } from 'lucide-react'
import { useCallback, useEffect, useState } from 'react'
import { addTokens, getUsers, type User } from '../api/services'
import { Button } from '../components/ui/Button'
import { Card } from '../components/ui/Card'

export function AdminUsers() {
  const [users, setUsers] = useState<User[]>([])
  const [error, setError] = useState<string | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [updatingId, setUpdatingId] = useState<string | null>(null)

  const loadUsers = useCallback(async () => {
    setIsLoading(true)
    setError(null)

    try {
      const data = await getUsers()
      setUsers(data.users)
    } catch {
      setError('Не удалось загрузить пользователей')
    } finally {
      setIsLoading(false)
    }
  }, [])

  useEffect(() => {
    loadUsers()
  }, [loadUsers])

  async function handleAddTokens(userId: string) {
    setUpdatingId(userId)

    try {
      const updated = await addTokens(userId, 5)
      setUsers((current) =>
        current.map((user) => (user.user_id === userId ? updated : user)),
      )
    } catch {
      setError('Не удалось начислить токены')
    } finally {
      setUpdatingId(null)
    }
  }

  return (
    <div>
      <div className="mb-8 flex items-end justify-between">
        <div>
          <h2 className="text-2xl font-semibold tracking-tight text-white">Пользователи</h2>
          <p className="mt-1 text-sm text-zinc-500">
            Управление подписками и балансом токенов клиентов
          </p>
        </div>
        <Button variant="secondary" onClick={loadUsers} disabled={isLoading}>
          <RefreshCw className={`h-4 w-4 ${isLoading ? 'animate-spin' : ''}`} />
          Обновить
        </Button>
      </div>

      {isLoading && (
        <p className="text-sm text-zinc-500">Загрузка...</p>
      )}

      {error && (
        <p className="mb-4 text-sm text-red-400">{error}</p>
      )}

      {!isLoading && !error && users.length === 0 && (
        <Card>
          <p className="text-sm text-zinc-400">Пользователей пока нет.</p>
        </Card>
      )}

      {!isLoading && users.length > 0 && (
        <Card className="overflow-x-auto p-0">
          <table className="w-full min-w-[640px] text-left text-sm">
            <thead>
              <tr className="border-b border-white/5 text-xs uppercase tracking-wider text-zinc-500">
                <th className="px-6 py-4 font-medium">Email</th>
                <th className="px-6 py-4 font-medium">Роль</th>
                <th className="px-6 py-4 font-medium">План</th>
                <th className="px-6 py-4 font-medium">Токены</th>
                <th className="px-6 py-4 font-medium">Действия</th>
              </tr>
            </thead>
            <tbody>
              {users.map((user) => (
                <tr key={user.user_id} className="border-b border-white/5 last:border-0">
                  <td className="px-6 py-4 text-zinc-200">{user.email}</td>
                  <td className="px-6 py-4 text-zinc-400">{user.role}</td>
                  <td className="px-6 py-4 text-zinc-400">{user.subscription_plan}</td>
                  <td className="px-6 py-4 font-medium text-indigo-300">{user.tokens}</td>
                  <td className="px-6 py-4">
                    <Button
                      variant="secondary"
                      className="h-8 px-3 text-xs"
                      onClick={() => handleAddTokens(user.user_id)}
                      disabled={updatingId === user.user_id}
                      isLoading={updatingId === user.user_id}
                    >
                      {updatingId !== user.user_id && <Coins className="h-3.5 w-3.5" />}
                      +5 Токенов
                    </Button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </Card>
      )}
    </div>
  )
}
