/// <reference types="node" />
import { readFileSync } from 'node:fs'
import { describe, expect, it } from 'vitest'

/**
 * Tests do not apply styles (jsdom has no layout), so a bug that only shows in a real browser needs another kind of guard:
 * this reads the stylesheet itself and checks that the rule that fixes it is still there.
 * It is read from disk because the test setup turns every imported stylesheet into an empty one (`css: false`).
 * The tests run from the frontend folder, so the path is relative to it.
 */
const css = readFileSync('src/index.css', 'utf8')

/** The declarations of the first rule whose selector contains the given text. */
function rule(selector: string): string {
  const start = css.indexOf(selector)
  if (start === -1) return ''
  const open = css.indexOf('{', start)
  return css.slice(open + 1, css.indexOf('}', open))
}

describe('the list that opens from a dropdown', () => {
  it('has the colors of the theme, so chalk text is never drawn on the browser\'s white', () => {
    const declarations = rule('select option')

    expect(declarations).toMatch(/background-color:\s*var\(--sheet\)/)
    expect(declarations).toMatch(/color:\s*var\(--ink\)/)
  })

  it('covers the group titles ("Gastos", "Ingresos") as well as the options', () => {
    expect(css).toMatch(/select option,\s*select optgroup\s*\{/)
  })

  it('shows the placeholder ("Elegí una categoría") in the soft color', () => {
    expect(rule('select option:disabled')).toMatch(/color:\s*var\(--ink-soft\)/)
  })
})

describe('the theme colors', () => {
  it('define the page and the sheet for both the page and the chalkboard', () => {
    for (const name of ['--paper', '--sheet', '--ink', '--ink-soft', '--pen', '--mark']) {
      const occurrences = css.split(`${name}:`).length - 1
      expect(occurrences, `${name} must be defined for the light theme and the dark one`).toBeGreaterThanOrEqual(2)
    }
  })
})

/** The color variables a theme declares, from the block that starts with the given selector (":root" or ".dark"). */
function themeColors(selector: string): Record<string, string> {
  const start = css.indexOf(`\n${selector} {`)
  const block = css.slice(start, css.indexOf('}', start))
  return Object.fromEntries([...block.matchAll(/--([\w-]+):\s*(#[0-9a-fA-F]{6})/g)].map((match) => [match[1], match[2]]))
}

function luminance(hex: string): number {
  const [r, g, b] = [1, 3, 5].map((i) => parseInt(hex.slice(i, i + 2), 16) / 255).map((v) => (v <= 0.03928 ? v / 12.92 : ((v + 0.055) / 1.055) ** 2.4))
  return 0.2126 * r + 0.7152 * g + 0.0722 * b
}

const contrast = (a: string, b: string) => {
  const [light, dark] = [luminance(a), luminance(b)].sort((x, y) => y - x)
  return (light + 0.05) / (dark + 0.05)
}

describe('the secondary text and the hints inside the fields', () => {
  it('has no transparency on the hint of a field: with it the contrast fell to 3:1', () => {
    expect(rule('.input::placeholder')).toMatch(/text-ink-soft;/)
    expect(rule('.input::placeholder')).not.toMatch(/text-ink-soft\//)
  })

  it.each([':root', '.dark'])('reaches 4.5:1 against the sheet and against the paper in %s', (selector) => {
    const colors = themeColors(selector)

    expect(contrast(colors['ink-soft'], colors.paper)).toBeGreaterThanOrEqual(4.5)
    expect(contrast(colors['ink-soft'], colors.sheet)).toBeGreaterThanOrEqual(4.5)
  })
})
