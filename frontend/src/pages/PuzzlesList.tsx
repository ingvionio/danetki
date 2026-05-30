import { Eye, EyeOff, RefreshCw } from 'lucide-react'
import { useCallback, useEffect, useState } from 'react'
import { getPuzzleAnswer, getPuzzles, type Puzzle } from '../api/services'
import { Button } from '../components/ui/Button'
import { Card } from '../components/ui/Card'
import { formatUnixDate } from '../lib/utils'
import { useAuthStore } from '../store/authStore'

type PuzzleCardProps = {
  puzzle: Puzzle
}

function PuzzleCard({ puzzle }: PuzzleCardProps) {
  const [revealed, setRevealed] = useState(false)
  const [hiddenPart, setHiddenPart] = useState<string | null>(null)
  const [isLoadingAnswer, setIsLoadingAnswer] = useState(false)
  const [answerError, setAnswerError] = useState<string | null>(null)

  async function toggleAnswer() {
    if (revealed) {
      setRevealed(false)
      return
    }

    if (hiddenPart) {
      setRevealed(true)
      return
    }

    setIsLoadingAnswer(true)
    setAnswerError(null)

    try {
      const data = await getPuzzleAnswer(puzzle.puzzle_id)
      setHiddenPart(data.hidden_part)
      setRevealed(true)
    } catch {
      setAnswerError('Не удалось загрузить ответ')
    } finally {
      setIsLoadingAnswer(false)
    }
  }

  return (
    <Card className="flex flex-col justify-between">
      <div>
        <p className="text-sm leading-relaxed text-zinc-200">{puzzle.open_part}</p>
        {revealed && hiddenPart && (
          <p className="mt-3 rounded-lg border border-zinc-700/60 bg-zinc-900/60 px-3 py-2 text-sm leading-relaxed text-amber-200/90">
            {hiddenPart}
          </p>
        )}
        {answerError && (
          <p className="mt-2 text-xs text-red-400">{answerError}</p>
        )}
      </div>
      <div className="mt-4 flex items-center justify-between gap-3">
        <p className="text-xs text-zinc-500">{formatUnixDate(puzzle.created_at)}</p>
        <Button
          variant="secondary"
          className="h-8 px-3 text-xs"
          onClick={toggleAnswer}
          disabled={isLoadingAnswer}
        >
          {isLoadingAnswer ? (
            'Загрузка...'
          ) : revealed ? (
            <>
              <EyeOff className="h-3.5 w-3.5" />
              Скрыть ответ
            </>
          ) : (
            <>
              <Eye className="h-3.5 w-3.5" />
              Показать ответ
            </>
          )}
        </Button>
      </div>
    </Card>
  )
}

export function PuzzlesList() {
  const role = useAuthStore((s) => s.role)
  const [puzzles, setPuzzles] = useState<Puzzle[]>([])
  const [total, setTotal] = useState(0)
  const [error, setError] = useState<string | null>(null)
  const [isLoading, setIsLoading] = useState(true)

  const loadPuzzles = useCallback(async () => {
    setIsLoading(true)
    setError(null)

    try {
      const data = await getPuzzles(1, 24)
      setPuzzles(data.puzzles)
      setTotal(data.total)
    } catch {
      setError('Не удалось загрузить данетки')
    } finally {
      setIsLoading(false)
    }
  }, [])

  useEffect(() => {
    loadPuzzles()
  }, [loadPuzzles])

  return (
    <div>
      <div className="mb-8 flex items-end justify-between">
        <div>
          <h2 className="text-2xl font-semibold tracking-tight text-white">Данетки</h2>
          <p className="mt-1 text-sm text-zinc-500">
            {total > 0 ? `${total} загадок в базе` : 'Каталог сгенерированных загадок'}
          </p>
        </div>
        <Button variant="secondary" onClick={loadPuzzles} disabled={isLoading}>
          <RefreshCw className={`h-4 w-4 ${isLoading ? 'animate-spin' : ''}`} />
          Обновить
        </Button>
      </div>

      {isLoading && (
        <p className="text-sm text-zinc-500">Загрузка...</p>
      )}

      {error && (
        <p className="text-sm text-red-400">{error}</p>
      )}

      {!isLoading && !error && puzzles.length === 0 && (
        <Card>
          <p className="text-sm text-zinc-400">
            {role === 'Admin'
              ? "Данеток пока нет. Перейдите в раздел 'Парсер', чтобы сгенерировать новые загадки."
              : 'Данеток пока нет. Администратор скоро добавит новые загадки. Загляните позже.'}
          </p>
        </Card>
      )}

      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
        {puzzles.map((puzzle) => (
          <PuzzleCard key={puzzle.puzzle_id} puzzle={puzzle} />
        ))}
      </div>
    </div>
  )
}
