import type { ListFilters } from './useListFilters.ts'

/** Hands a file to the browser as a download, as if the user had clicked a link to it. */
export function downloadBlob(blob: Blob, fileName: string): void {
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = fileName
  document.body.append(link)
  link.click()
  link.remove()
  // Give the browser a moment to start the download before the address stops being valid
  setTimeout(() => URL.revokeObjectURL(url), 1000)
}

/** The name of the exported file: it carries the month when the list follows one, so downloads from different months differ. */
export function exportFileName(filters: Pick<ListFilters, 'rangeMode' | 'month'>): string {
  return filters.rangeMode ? 'movimientos.csv' : `movimientos-${filters.month}.csv`
}
