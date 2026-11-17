/**
 * The movement that was just added, so its row can be highlighted in the ledger for a moment. It travels in the query
 * cache (the form writes it, the list reads it): the two do not know each other, and the cache is already what they share.
 */
export const JUST_ADDED_KEY = ['justAdded'] as const

/** How long the row stays marked: the same as the animation in index.css, so the mark is gone when the highlighter has faded. */
export const JUST_ADDED_MS = 2000
