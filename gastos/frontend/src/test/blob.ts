import { expect } from 'vitest'

/**
 * True for a Blob (the contents of a file) whatever environment created it.
 *
 * `instanceof Blob` is not reliable in these tests: `fetch` and `Response` come from Node and the global `Blob` comes from
 * jsdom, and depending on the Node version they are the same class (it passed on Node 24) or two different ones
 * (it failed on Node 22, which is what the CI runs). The tag every Blob carries is the same in both.
 */
export function isBlob(value: unknown): boolean {
  return Object.prototype.toString.call(value) === '[object Blob]'
}

/** For `toHaveBeenCalledWith`: anything shaped like a Blob, from either environment. */
export function anyBlob() {
  return expect.objectContaining({
    size: expect.any(Number),
    type: expect.any(String),
    arrayBuffer: expect.any(Function),
  })
}
