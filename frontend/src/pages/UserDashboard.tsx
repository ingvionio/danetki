import { Eye, EyeOff, LogOut, Sparkles } from 'lucide-react'
import { useCallback, useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import {
  consumeToken,
  getMe,
  getPuzzleAnswer,
  getRandomPuzzle,
  type Puzzle,
  type User,
} from '../api/services'
import { CopyButton } from '../components/CopyButton'
import { TelegramExportButton } from '../components/TelegramExportButton'
import { Button } from '../components/ui/Button'
import { useCopyToClipboard } from '../lib/useCopyToClipboard'
import { cn } from '../lib/utils'
import { useAuthStore } from '../store/authStore'

export function UserDashboard() {
  const navigate = useNavigate()
  const clearAuth = useAuthStore((s) => s.clearAuth)
  const { copiedKey, copy } = useCopyToClipboard()

  const [profile, setProfile] = useState<User | null>(null)
  const [puzzle, setPuzzle] = useState<Puzzle | null>(null)
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [revealed, setRevealed] = useState(false)
  const [hiddenPart, setHiddenPart] = useState<string | null>(null)
  const [isLoadingAnswer, setIsLoadingAnswer] = useState(false)
  const [answerError, setAnswerError] = useState<string | null>(null)

  const loadProfile = useCallback(async () => {
    try {
      const user = await getMe()
      setProfile(user)
    } catch {
      clearAuth()
      navigate('/login', { replace: true })
    }
  }, [clearAuth, navigate])

  useEffect(() => {
    loadProfile()
  }, [loadProfile])

  async function handleGenerate() {
    setIsLoading(true)
    setError(null)
    setRevealed(false)
    setHiddenPart(null)
    setAnswerError(null)

    try {
      const tokenResult = await consumeToken()

      if (!tokenResult.success) {
        setError('Токены закончились. Оформите подписку Pro.')
        setProfile((current) =>
          current ? { ...current, tokens: tokenResult.remaining_tokens } : current,
        )
        return
      }

      setProfile((current) =>
        current ? { ...current, tokens: tokenResult.remaining_tokens } : current,
      )

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

  function handleLogout() {
    clearAuth()
    navigate('/login')
  }

  return (
    <div className="relative flex min-h-screen flex-col overflow-hidden bg-zinc-950">
      <div
        aria-hidden
        className="pointer-events-none absolute inset-0 bg-[radial-gradient(ellipse_at_top,_rgba(79,70,229,0.18),_transparent_55%)]"
      />

      <header className="relative z-10 border-b border-white/5 bg-zinc-950/80 backdrop-blur">
        <div className="mx-auto flex max-w-3xl items-center justify-between gap-4 px-4 py-4">
          <div>
            <p className="text-xs font-medium uppercase tracking-[0.2em] text-indigo-400/80">
              Danetka
            </p>
            {profile && (
              <p className="mt-1 text-sm text-zinc-400">
                План: <span className="text-zinc-200">{profile.subscription_plan}</span>
                {' | '}
                Токенов: <span className="font-medium text-indigo-300">{profile.tokens}</span>
              </p>
            )}
          </div>
          <Button variant="secondary" className="h-9 px-3 text-xs" onClick={handleLogout}>
            <LogOut className="h-3.5 w-3.5" />
            Выйти
          </Button>
        </div>
      </header>

      <main className="relative z-10 flex flex-1 flex-col items-center justify-center px-4 py-12">
        <div className="flex w-full max-w-xl flex-col items-center text-center">
          <h1 className="text-3xl font-semibold tracking-tight text-white sm:text-4xl">
            Генератор данеток
          </h1>

          {!puzzle && !error && (
            <p className="mt-4 max-w-sm text-sm leading-relaxed text-zinc-500">
              Каждая генерация списывает 1 токен. Получите готовый пост для Telegram за пару кликов.
            </p>
          )}

          <div className={cn('mt-10 w-full transition-all duration-500', puzzle && 'mt-8')}>
            {puzzle && (
              <div className="mb-8 rounded-2xl border border-white/10 bg-zinc-900/70 p-8 text-left shadow-2xl shadow-indigo-950/40 backdrop-blur">
                <p className="text-xs font-medium uppercase tracking-wider text-zinc-500">
                  Ваша данетка
                </p>

                <div className="relative mt-4">
                  <CopyButton
                    active={copiedKey === 'open'}
                    onClick={() => copy('open', puzzle.open_part)}
                    label="Скопировать пост"
                    className="absolute right-0 top-0"
                  />
                  <p className="pr-10 text-lg leading-relaxed text-zinc-100">{puzzle.open_part}</p>
                </div>

                {revealed && hiddenPart && (
                  <div className="relative mt-4 rounded-xl border border-amber-500/20 bg-amber-500/5 px-4 py-3">
                    <CopyButton
                      active={copiedKey === 'answer'}
                      onClick={() => copy('answer', hiddenPart)}
                      label="Скопировать ответ"
                      className="absolute right-2 top-2"
                    />
                    <p className="pr-10 text-base leading-relaxed text-amber-100/90">{hiddenPart}</p>
                  </div>
                )}

                {answerError && (
                  <p className="mt-3 text-sm text-red-400">{answerError}</p>
                )}

                {puzzle.source_url && (
                  <a
                    href={puzzle.source_url}
                    target="_blank"
                    rel="noopener noreferrer"
                    className="mt-4 inline-block text-xs text-indigo-400 hover:text-indigo-300"
                  >
                    Источник
                  </a>
                )}

                <div className="mt-6 flex flex-wrap items-center justify-center gap-3">
                  <TelegramExportButton
                    puzzle={puzzle}
                    hiddenPart={hiddenPart}
                    onAnswerLoaded={(answer) => {
                      setHiddenPart(answer)
                      setRevealed(true)
                    }}
                    copyKey="telegram"
                  />
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

            <div className="flex justify-center">
              <Button
                onClick={handleGenerate}
                isLoading={isLoading}
                disabled={!profile}
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
      </main>
    </div>
  )
}
