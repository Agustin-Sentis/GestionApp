import { afterEach, describe, expect, it, vi } from 'vitest'
import { downloadBlob, exportFileName } from './download.ts'

describe('downloadBlob', () => {
  afterEach(() => {
    vi.restoreAllMocks()
    vi.useRealTimers()
  })

  it('clicks a temporary link to the file with the given name, then removes it and frees the address', () => {
    vi.useFakeTimers()
    URL.createObjectURL = vi.fn(() => 'blob:fake-address')
    URL.revokeObjectURL = vi.fn()
    let wasInThePage = false
    const click = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {
      // Some browsers only download links that are in the page
      wasInThePage = document.querySelector('a[download]') !== null
    })

    downloadBlob(new Blob(['a;b']), 'movimientos-2026-11.csv')

    const clicked = click.mock.contexts[0] as HTMLAnchorElement
    expect(wasInThePage).toBe(true)
    expect(clicked.getAttribute('download')).toBe('movimientos-2026-11.csv')
    expect(clicked.getAttribute('href')).toBe('blob:fake-address')
    expect(document.body.contains(clicked)).toBe(false) // nothing left behind
    expect(URL.revokeObjectURL).not.toHaveBeenCalled() // the browser still needs the address
    vi.runAllTimers()
    expect(URL.revokeObjectURL).toHaveBeenCalledWith('blob:fake-address')
  })
})

describe('exportFileName', () => {
  it('carries the month when the list follows a month', () => {
    expect(exportFileName({ rangeMode: false, month: '2026-03' })).toBe('movimientos-2026-03.csv')
  })

  it('has no month when the list uses a date range', () => {
    expect(exportFileName({ rangeMode: true, month: '2026-03' })).toBe('movimientos.csv')
  })
})
