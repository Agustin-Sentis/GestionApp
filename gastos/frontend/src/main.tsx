import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { BrowserRouter } from 'react-router-dom'
import App from './App.tsx'
import AuthContextProvider from './Services/authContext/AuthContextProvider.tsx'
import ErrorBoundary from './components/ErrorBoundary/ErrorBoundary.tsx'
import ToastContextProvider from './Services/toastContext/ToastContextProvider.tsx'
import './index.css'

const queryClient = new QueryClient({
  defaultOptions: {
    queries: { retry: 1, refetchOnWindowFocus: false },
  },
})

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    {/* Outermost, so even a failure in a provider shows the error screen instead of a blank page */}
    <ErrorBoundary>
      <QueryClientProvider client={queryClient}>
        <AuthContextProvider>
          <ToastContextProvider>
            <BrowserRouter>
              <App />
            </BrowserRouter>
          </ToastContextProvider>
        </AuthContextProvider>
      </QueryClientProvider>
    </ErrorBoundary>
  </StrictMode>,
)
