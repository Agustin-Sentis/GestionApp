// Applies the saved theme before the first paint, so a dark page does not flash white while the app loads.
// Same rule as src/theme.ts: "light" and "dark" are stored; anything else follows the system.
// It is a file (not a script inside index.html) so the Content-Security-Policy can allow scripts from the app only.
try {
  var stored = localStorage.getItem('gastos.theme')
  var dark = stored === 'dark' || (stored !== 'light' && window.matchMedia('(prefers-color-scheme: dark)').matches)
  document.documentElement.classList.toggle('dark', dark)
  document.documentElement.style.colorScheme = dark ? 'dark' : 'light'
} catch {
  // No localStorage (private mode, etc.): the theme follows the system and the app takes over once it loads
}
