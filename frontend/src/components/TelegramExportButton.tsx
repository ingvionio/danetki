import { Check, Send } from 'lucide-react'
import { useState } from 'react'
import { getPuzzleAnswer, type Puzzle } from '../api/services'
import { formatTelegramPost } from '../lib/formatTelegramPost'
import { useCopyToClipboard } from '../lib/useCopyToClipboard'
import { cn } from '../lib/utils'
import { Button } from './ui/Button'

type TelegramExportButtonProps = {
  puzzle: Puzzle
  hiddenPart: string | null
  onAnswerLoaded: (answer: string) => void
  copyKey: string
  className?: string
}

export function TelegramExportButton({
  puzzle,
  hiddenPart,
  onAnswerLoaded,
  copyKey,
  className,
}: TelegramExportButtonProps) {
  const { copiedKey, copy } = useCopyToClipboard()
  const [isExporting, setIsExporting] = useState(false)
  const [exportError, setExportError] = useState<string | null>(null)
  const isCopied = copiedKey === copyKey

  async function handleExport() {
    setIsExporting(true)
    setExportError(null)

    try {
      let answer = hiddenPart

      if (!answer) {
        const data = await getPuzzleAnswer(puzzle.puzzle_id)
        answer = data.hidden_part
        onAnswerLoaded(answer)
      }

      await copy(copyKey, formatTelegramPost(puzzle.open_part, answer, puzzle.source_url))
    } catch {
      setExportError('Не удалось скопировать')
    } finally {
      setIsExporting(false)
    }
  }

  return (
    <div className={cn('flex flex-col items-end gap-1', className)}>
      <Button
        variant="secondary"
        onClick={handleExport}
        disabled={isExporting}
        isLoading={isExporting}
        className="h-8 shrink-0 px-3 text-xs sm:h-9 sm:text-sm"
      >
        {!isExporting && !isCopied && <Send className="h-3.5 w-3.5" />}
        {!isExporting && isCopied && <Check className="h-3.5 w-3.5" />}
        {isCopied ? 'Скопировано' : 'Скопировать для TG'}
      </Button>
      {exportError && (
        <p className="text-xs text-red-400">{exportError}</p>
      )}
    </div>
  )
}
