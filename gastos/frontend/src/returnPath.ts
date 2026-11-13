/** Where the signed-in dashboard lives. The root address (/) still works and sends a signed-in visitor here. */
export const PANEL_PATH = '/panel'

/**
 * Where to go after signing in: the page the visitor was trying to open (kept in the navigation state by RequireAuth), or the
 * dashboard. Only a path INSIDE the app is accepted: anything that could lead to another site falls back to the dashboard.
 */
export function returnPathFrom(state: unknown): string {
  const from = (state as { from?: unknown } | null | undefined)?.from
  if (typeof from !== 'string') return PANEL_PATH
  // "//host" and "/\host" are read by browsers as another site; "/login…" would send them back to the form they just used
  const insideTheApp = from.startsWith('/') && !from.startsWith('//') && !from.startsWith('/\\')
  return insideTheApp && !from.startsWith('/login') ? from : PANEL_PATH
}
