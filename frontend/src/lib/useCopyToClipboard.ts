import { useCallback, useRef, useState } from 'react'

export function useCopyToClipboard() {
  const [copiedKey, setCopiedKey] = useState<string | null>(null)
  const timeoutRef = useRef<number | null>(null)

  const copy = useCallback(async (key: string, text: string) => {
    await navigator.clipboard.writeText(text)

    if (timeoutRef.current) {
      window.clearTimeout(timeoutRef.current)
    }

    setCopiedKey(key)
    timeoutRef.current = window.setTimeout(() => {
      setCopiedKey(null)
      timeoutRef.current = null
    }, 2000)
  }, [])

  return { copiedKey, copy }
}
