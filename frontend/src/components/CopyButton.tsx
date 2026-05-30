import { Check, Copy } from 'lucide-react'
import { cn } from '../lib/utils'

type CopyButtonProps = {
  active: boolean
  onClick: () => void
  label: string
  className?: string
}

export function CopyButton({ active, onClick, label, className }: CopyButtonProps) {
  return (
    <button
      type="button"
      onClick={onClick}
      aria-label={label}
      className={cn(
        'rounded-md p-1 text-zinc-500 transition-colors hover:text-zinc-300',
        className,
      )}
    >
      {active ? <Check className="h-4 w-4" /> : <Copy className="h-4 w-4" />}
    </button>
  )
}
