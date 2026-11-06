import { vi } from 'vitest'

export interface MockReply {
  status?: number
  body?: unknown
}

export type MockHandler = (url: string, init: RequestInit) => MockReply | Promise<MockReply>

/**
 * Replaces the global fetch with a fake that answers with JSON, so tests never touch the network.
 * Returns the fake so a test can check what was requested (URL, method, headers, body).
 */
export function mockFetch(handler: MockHandler) {
  const fake = vi.fn(async (input: RequestInfo | URL, init: RequestInit = {}) => {
    const { status = 200, body } = await handler(String(input), init)
    // A 204 (and 205/304) cannot carry a body, and Response refuses to be built with one
    const hasBody = body !== undefined && status !== 204
    return new Response(hasBody ? JSON.stringify(body) : null, {
      status,
      headers: { 'Content-Type': 'application/json' },
    })
  })
  vi.stubGlobal('fetch', fake)
  return fake
}

/**
 * Like mockFetch, but answers per route: keys are "METHOD /path" (the query string is ignored), e.g. "GET /api/category".
 * A request nobody mocked is answered with a 500 that names it, so a forgotten route fails the test with a clear message
 * instead of returning data of the wrong shape to some other query.
 */
export function mockApi(routes: Record<string, (init: RequestInit, url: URL) => MockReply | Promise<MockReply>>) {
  return mockFetch((rawUrl, init) => {
    const url = new URL(rawUrl)
    const key = `${(init.method ?? 'GET').toUpperCase()} ${url.pathname}`
    const route = routes[key]
    return route ? route(init, url) : { status: 500, body: { error: `Unmocked request: ${key}` } }
  })
}

/** A fetch that fails like an unreachable server does (no response at all). */
export function mockFetchNetworkError() {
  const fake = vi.fn(async () => {
    throw new TypeError('Failed to fetch')
  })
  vi.stubGlobal('fetch', fake)
  return fake
}

/** Headers of a recorded call, as a plain object that is easy to assert on. */
export function headersOf(init: RequestInit | undefined): Record<string, string> {
  return Object.fromEntries(new Headers(init?.headers).entries())
}
