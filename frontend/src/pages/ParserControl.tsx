import { RefreshCw } from 'lucide-react'
import { type FormEvent, useCallback, useEffect, useState } from 'react'
import {
  getParserStatus,
  listParserJobs,
  startParser,
  type ParserJob,
  type ParserStatusResponse,
} from '../api/services'
import { Button } from '../components/ui/Button'
import { Card } from '../components/ui/Card'
import { Input } from '../components/ui/Input'
import { cn, formatUnixDateTime } from '../lib/utils'

const TERMINAL_STATUSES = new Set(['STATUS_DONE', 'STATUS_FAILED'])

const STATUS_LABELS: Record<string, string> = {
  STATUS_RUNNING: 'В работе',
  STATUS_DONE: 'Готово',
  STATUS_FAILED: 'Ошибка',
  STATUS_UNKNOWN: 'Неизвестно',
}

function statusLabel(status: string): string {
  return STATUS_LABELS[status] ?? status
}

function statusClassName(status: string): string {
  switch (status) {
    case 'STATUS_RUNNING':
      return 'text-amber-300'
    case 'STATUS_DONE':
      return 'text-emerald-400'
    case 'STATUS_FAILED':
      return 'text-red-400'
    default:
      return 'text-zinc-400'
  }
}

function shortJobId(jobId: string): string {
  return jobId.slice(0, 8)
}

function isAiProcessing(job: { status: string; total_queued: number; puzzles_created: number }): boolean {
  return (
    !TERMINAL_STATUSES.has(job.status) ||
    (job.total_queued > 0 && job.puzzles_created < job.total_queued)
  )
}

export function ParserControl() {
  const [sourceUrl, setSourceUrl] = useState('https://factroom.ru/')
  const [limit, setLimit] = useState('10')
  const [jobs, setJobs] = useState<ParserJob[]>([])
  const [jobsTotal, setJobsTotal] = useState(0)
  const [selectedJobId, setSelectedJobId] = useState<string | null>(null)
  const [status, setStatus] = useState<ParserStatusResponse | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [isStarting, setIsStarting] = useState(false)
  const [isLoadingJobs, setIsLoadingJobs] = useState(true)

  const loadJobs = useCallback(async () => {
    try {
      const data = await listParserJobs(1, 20)
      setJobs(data.jobs)
      setJobsTotal(data.total)
      setError(null)
    } catch {
      setError('Не удалось загрузить историю заявок')
    } finally {
      setIsLoadingJobs(false)
    }
  }, [])

  useEffect(() => {
    loadJobs()
  }, [loadJobs])

  const hasActiveProcessing = jobs.some(isAiProcessing)
  const isSelectedProcessing = status && isAiProcessing(status)

  useEffect(() => {
    if (!hasActiveProcessing && !isSelectedProcessing) return

    const timer = window.setInterval(loadJobs, 3000)
    return () => window.clearInterval(timer)
  }, [hasActiveProcessing, isSelectedProcessing, loadJobs])

  useEffect(() => {
    if (!selectedJobId) {
      setStatus(null)
      return
    }

    let active = true

    async function poll() {
      try {
        const next = await getParserStatus(selectedJobId!)
        if (active) {
          setStatus(next)
          setError(null)
        }
      } catch {
        if (active) setError('Не удалось получить статус парсера')
      }
    }

    poll()
    const timer = window.setInterval(poll, 3000)
    return () => {
      active = false
      window.clearInterval(timer)
    }
  }, [selectedJobId])

  async function handleStart(event: FormEvent) {
    event.preventDefault()
    setError(null)
    setIsStarting(true)
    setStatus(null)

    try {
      const response = await startParser(Number(limit), sourceUrl)
      setSelectedJobId(response.job_id)
      await loadJobs()
    } catch {
      setError('Не удалось запустить парсер')
    } finally {
      setIsStarting(false)
    }
  }

  const aiProgress =
    status && status.total_queued > 0
      ? Math.min(100, Math.round((status.puzzles_created / status.total_queued) * 100))
      : 0

  const isRunning = status && !TERMINAL_STATUSES.has(status.status)

  return (
    <div className="mx-auto max-w-4xl">
      <div className="mb-8">
        <h2 className="text-2xl font-semibold tracking-tight text-white">Парсер</h2>
        <p className="mt-1 text-sm text-zinc-500">
          Сбор историй → Kafka → генерация данеток через ИИ
        </p>
      </div>

      <Card>
        <form onSubmit={handleStart} className="space-y-4">
          <div className="space-y-2">
            <label htmlFor="sourceUrl" className="text-sm font-medium text-zinc-300">
              URL источника
            </label>
            <Input
              id="sourceUrl"
              value={sourceUrl}
              onChange={(e) => setSourceUrl(e.target.value)}
              placeholder="https://factroom.ru/"
              required
            />
          </div>

          <div className="space-y-2">
            <label htmlFor="limit" className="text-sm font-medium text-zinc-300">
              Лимит историй
            </label>
            <Input
              id="limit"
              type="number"
              min={1}
              max={100}
              value={limit}
              onChange={(e) => setLimit(e.target.value)}
              required
            />
          </div>

          {error && <p className="text-sm text-red-400">{error}</p>}

          <Button type="submit" isLoading={isStarting} disabled={Boolean(isRunning)}>
            Запустить
          </Button>
        </form>
      </Card>

      <Card className="mt-6">
        <div className="mb-4 flex items-center justify-between">
          <div>
            <h3 className="text-sm font-medium text-zinc-200">История заявок</h3>
            <p className="mt-0.5 text-xs text-zinc-500">
              {jobsTotal > 0 ? `${jobsTotal} заявок всего` : 'Заявок пока нет'}
            </p>
          </div>
          <Button variant="secondary" className="h-8 px-3 text-xs" onClick={loadJobs}>
            <RefreshCw className={`h-3.5 w-3.5 ${isLoadingJobs ? 'animate-spin' : ''}`} />
            Обновить
          </Button>
        </div>

        {isLoadingJobs && jobs.length === 0 && (
          <p className="text-sm text-zinc-500">Загрузка...</p>
        )}

        {!isLoadingJobs && jobs.length === 0 && (
          <p className="text-sm text-zinc-400">Запустите парсер — заявки появятся здесь.</p>
        )}

        {jobs.length > 0 && (
          <div className="overflow-x-auto">
            <table className="w-full min-w-[720px] text-left text-sm">
              <thead>
                <tr className="border-b border-zinc-800 text-xs text-zinc-500">
                  <th className="pb-2 pr-3 font-medium">Дата</th>
                  <th className="pb-2 pr-3 font-medium">ID</th>
                  <th className="pb-2 pr-3 font-medium">Источник</th>
                  <th className="pb-2 pr-3 font-medium">Лимит</th>
                  <th className="pb-2 pr-3 font-medium">Статус</th>
                  <th className="pb-2 pr-3 font-medium">Найдено</th>
                  <th className="pb-2 pr-3 font-medium">В Kafka</th>
                  <th className="pb-2 font-medium">Данеток</th>
                </tr>
              </thead>
              <tbody>
                {jobs.map((job) => (
                  <tr
                    key={job.job_id}
                    onClick={() => setSelectedJobId(job.job_id)}
                    className={cn(
                      'cursor-pointer border-b border-zinc-800/60 transition-colors hover:bg-zinc-800/40',
                      selectedJobId === job.job_id && 'bg-zinc-800/60',
                    )}
                  >
                    <td className="py-2.5 pr-3 text-zinc-300">{formatUnixDateTime(job.started_at)}</td>
                    <td className="py-2.5 pr-3 font-mono text-xs text-zinc-400">
                      {shortJobId(job.job_id)}
                    </td>
                    <td className="max-w-[180px] truncate py-2.5 pr-3 text-zinc-300" title={job.source_url}>
                      {job.source_url}
                    </td>
                    <td className="py-2.5 pr-3 text-zinc-300">{job.limit}</td>
                    <td className={cn('py-2.5 pr-3', statusClassName(job.status))}>
                      {statusLabel(job.status)}
                    </td>
                    <td className="py-2.5 pr-3 text-zinc-300">{job.total_found}</td>
                    <td className="py-2.5 pr-3 text-zinc-300">{job.total_queued}</td>
                    <td className="py-2.5 text-emerald-300">
                      {job.puzzles_created}
                      {job.total_queued > 0 && (
                        <span className="text-zinc-500"> / {job.total_queued}</span>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Card>

      {status && (
        <Card className="mt-6 space-y-4">
          <div className="flex items-center justify-between text-sm">
            <span className="text-zinc-400">Job ID</span>
            <span className="font-mono text-zinc-200">{status.job_id}</span>
          </div>

          <div className="flex items-center justify-between text-sm">
            <span className="text-zinc-400">Статус парсера</span>
            <span className={statusClassName(status.status)}>{statusLabel(status.status)}</span>
          </div>

          <div className="space-y-2">
            <div className="flex flex-wrap justify-between gap-2 text-sm text-zinc-400">
              <span>Найдено: {status.total_found}</span>
              <span>В Kafka: {status.total_queued}</span>
              <span className="text-emerald-300">
                Данеток готово: {status.puzzles_created}
                {status.total_queued > 0 ? ` / ${status.total_queued}` : ''}
              </span>
            </div>
            <div className="h-2 overflow-hidden rounded-full bg-zinc-800">
              <div
                className="h-full rounded-full bg-emerald-500 transition-all duration-500"
                style={{ width: `${aiProgress}%` }}
              />
            </div>
            <p className="text-xs text-zinc-500">
              Прогресс ИИ: сколько историй из Kafka уже превращено в данетки
            </p>
          </div>

          {status.error && (
            <p className="text-sm text-red-400">{status.error}</p>
          )}
        </Card>
      )}
    </div>
  )
}
