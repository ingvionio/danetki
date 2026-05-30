export function formatTelegramPost(
  openPart: string,
  hiddenPart: string,
  sourceUrl?: string,
): string {
  let text = `❓ ЗАГАДКА:\n${openPart}\n\n💡 ОТВЕТ:\n${hiddenPart}`

  if (sourceUrl) {
    text += `\n\n🔗 Источник: ${sourceUrl}`
  }

  return text
}
