import tailwindcss from '@tailwindcss/vite'
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vitest/config'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react(), tailwindcss()],
  build: {
    // Never inline assets as data: URIs: the CSP (`font-src 'self'`) blocks a font embedded that way
    // (Vite inlined one tiny Martian Mono subset by default)
    assetsInlineLimit: 0,
  },
  test: {
    // A browser-like DOM, so React components can be rendered and queried like a user would
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.ts'],
    // Styles are not needed to check behavior
    css: false,
    include: ['src/**/*.test.{ts,tsx}'],
  },
})
