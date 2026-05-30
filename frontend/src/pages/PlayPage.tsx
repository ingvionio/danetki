import { Eye, EyeOff, Sparkles } from 'lucide-react'
import { useState } from 'react'
import { Link } from 'react-router-dom'
import { getPuzzleAnswer, getRandomPuzzle, type Puzzle } from '../api/services'
import { Button } from '../components/ui/Button'
import { cn } from '../lib/utils'

export function PlayPage() {
  const [puzzle, setPuzzle] = useState<Puzzle | null>(null)
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [revealed, setRevealed] = useState(false)
  const [hiddenPart, setHiddenPart] = useState<string | null>(null)
  const [isLoadingAnswer, setIsLoadingAnswer] = useState(false)
  const [answerError, setAnswerError] = useState<string | null>(null)

  async function handleGenerate() {
    setIsLoading(true)
    setError(null)
    setRevealed(false)
    setHiddenPart(null)
    setAnswerError(null)

    try {
      const next = await getRandomPuzzle()
      setPuzzle(next)
    } catch {
      setError('Не удалось получить данетку. Попробуйте ещё раз.')
      setPuzzle(null)
    } finally {
      setIsLoading(false)
    }
  }

  async function toggleAnswer() {
    if (!puzzle) return

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
    <div className="relative flex min-h-screen flex-col items-center justify-center overflow-hidden bg-zinc-950 px-4 py-12">
      <div
        aria-hidden
        className="pointer-events-none absolute inset-0 bg-[radial-gradient(ellipse_at_top,_rgba(79,70,229,0.18),_transparent_55%)]"
      />
      <div
        aria-hidden
        className="pointer-events-none absolute bottom-0 left-1/2 h-64 w-[36rem] -translate-x-1/2 bg-[radial-gradient(ellipse,_rgba(99,102,241,0.12),_transparent_70%)]"
      />

      <Link
        to="/login"
        className="absolute right-6 top-6 text-xs text-zinc-600 transition-colors hover:text-zinc-400"
      >
        Вход для администратора
      </Link>

      <div className="relative z-10 flex w-full max-w-xl flex-col items-center text-center">
        <p className="mb-2 text-xs font-medium uppercase tracking-[0.2em] text-indigo-400/80">
          Danetka
        </p>
        <h1 className="text-3xl font-semibold tracking-tight text-white sm:text-4xl">
          Генератор данеток
        </h1>

        {!puzzle && !error && (
          <p className="mt-4 max-w-sm text-sm leading-relaxed text-zinc-500">
            Нажмите кнопку — и получите случайную загадку из нашей коллекции.
          </p>
        )}

        <div
          className={cn(
            'mt-10 w-full transition-all duration-500',
            puzzle && 'mt-8',
          )}
        >
          {puzzle && (
            <div className="mb-8 rounded-2xl border border-white/10 bg-zinc-900/70 p-8 shadow-2xl shadow-indigo-950/40 backdrop-blur transition-all duration-500">
              <p className="text-xs font-medium uppercase tracking-wider text-zinc-500">
                Ваша данетка
              </p>
              <p className="mt-4 text-left text-lg leading-relaxed text-zinc-100">
                {puzzle.open_part}
              </p>
              {revealed && hiddenPart && (
                <p className="mt-4 rounded-xl border border-amber-500/20 bg-amber-500/5 px-4 py-3 text-left text-base leading-relaxed text-amber-100/90">
                  {hiddenPart}
                </p>
              )}
              {answerError && (
                <p className="mt-3 text-left text-sm text-red-400">{answerError}</p>
              )}
              <div className="mt-6 flex justify-center">
                <Button
                  variant="secondary"
                  onClick={toggleAnswer}
                  disabled={isLoadingAnswer}
                  className="rounded-full px-5"
                >
                  {isLoadingAnswer ? (
                    'Загрузка...'
                  ) : revealed ? (
                    <>
                      <EyeOff className="h-4 w-4" />
                      Скрыть ответ
                    </>
                  ) : (
                    <>
                      <Eye className="h-4 w-4" />
                      Показать ответ
                    </>
                  )}
                </Button>
              </div>
            </div>
          )}

          {error && (
            <p className="mb-6 text-sm text-red-400">{error}</p>
          )}

          <Button
            onClick={handleGenerate}
            isLoading={isLoading}
            className={cn(
              'h-14 min-w-[220px] rounded-full px-8 text-base font-medium shadow-lg shadow-indigo-900/40',
              'bg-indigo-600 hover:bg-indigo-500 hover:shadow-indigo-800/50',
              !puzzle && 'h-16 min-w-[260px] text-lg',
            )}
          >
            {!isLoading && <Sparkles className="h-5 w-5" />}
            {puzzle ? 'Ещё одна' : 'Сгенерировать'}
          </Button>
        </div>
      </div>
    </div>
  )
}
