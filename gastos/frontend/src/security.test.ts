/// <reference types="node" />
import { existsSync, readFileSync } from 'node:fs'
import { describe, expect, it } from 'vitest'

/**
 * The Content-Security-Policy lives in vercel.json, and a browser is the only place where it really runs. What can be guarded here
 * is what would break it silently: a script written inline in the page (a policy without 'unsafe-inline' blocks it), a policy
 * that opens scripts to anything, or one that forgets the API. The tests run from the frontend folder.
 */
const API_ORIGIN = 'https://gestionapp-j0j4.onrender.com'

const html = readFileSync('index.html', 'utf8')
const vercel = JSON.parse(readFileSync('vercel.json', 'utf8')) as {
  headers: { source: string; headers: { key: string; value: string }[] }[]
}

const headers = vercel.headers.find((h) => h.source === '/(.*)')?.headers ?? []
const headerValue = (name: string) => headers.find((h) => h.key.toLowerCase() === name.toLowerCase())?.value

/** The policy as a map: directive name -> its sources. */
function policy(value: string | undefined): Record<string, string[]> {
  return Object.fromEntries(
    (value ?? '')
      .split(';')
      .map((part) => part.trim().split(/\s+/))
      .filter((parts) => parts[0])
      .map(([name, ...sources]) => [name, sources]),
  )
}

// Report-Only first: the browser reports what it WOULD block (in the console) without blocking anything. Once a session of
// real use shows no violations, the header is renamed to Content-Security-Policy (see the README).
const csp = policy(headerValue('Content-Security-Policy-Report-Only') ?? headerValue('Content-Security-Policy'))

describe('index.html', () => {
  it('has no inline script: every script is a file of the app', () => {
    const scripts = [...html.matchAll(/<script\b([^>]*)>([\s\S]*?)<\/script>/gi)]

    expect(scripts.length).toBeGreaterThan(0)
    for (const [, attributes, body] of scripts) {
      expect(attributes, 'a script without src is inline').toMatch(/\bsrc=/)
      expect(body.trim()).toBe('')
    }
  })

  it('loads the theme before the first paint, from a file in public/', () => {
    expect(html).toMatch(/<script src="\/theme-init\.js"><\/script>/)
    expect(existsSync('public/theme-init.js')).toBe(true)
  })
})

describe('vercel.json Content-Security-Policy', () => {
  it('exists', () => {
    expect(Object.keys(csp).length).toBeGreaterThan(0)
  })

  it('runs scripts only from the app itself', () => {
    expect(csp['script-src']).toEqual(["'self'"])
  })

  it('does not fall back to anything open', () => {
    expect(csp['default-src']).toEqual(["'self'"])
    expect(csp['object-src']).toEqual(["'none'"])
    expect(csp['base-uri']).toEqual(["'self'"])
    expect(csp['frame-ancestors']).toEqual(["'none'"])
  })

  it('lets the app talk to its API, and to nobody else', () => {
    expect(csp['connect-src']).toEqual(["'self'", API_ORIGIN])
  })

  it('allows what the app really loads: its own fonts and images, and the inline data icon of the page', () => {
    expect(csp['font-src']).toEqual(["'self'"])
    expect(csp['img-src']).toEqual(["'self'", 'data:'])
  })

  it('keeps the other security headers', () => {
    expect(headerValue('X-Content-Type-Options')).toBe('nosniff')
    expect(headerValue('X-Frame-Options')).toBe('DENY')
    expect(headerValue('Referrer-Policy')).toBe('strict-origin-when-cross-origin')
  })
})
