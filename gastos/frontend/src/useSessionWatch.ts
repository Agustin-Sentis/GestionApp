import { useQuery } from '@tanstack/react-query'
import { api } from './api.ts'

/**
 * Checks the session each time the user comes back to the tab. The server can end a session on its own (the password was reset
 * somewhere else), but a page that is already loaded makes no request until the user does something, so it would keep showing
 * the account until a reload. Asking for the settings on focus is a cheap request that answers 401 for a closed session, and
 * the app signs out on that. It also refreshes what the settings hold (for example, the email confirmed in another tab).
 * Everything else keeps its own "no refetch on focus": only this query opts in.
 */
export function useSessionWatch() {
  useQuery({ queryKey: ['settings'], queryFn: api.settings.get, refetchOnWindowFocus: true })
}
