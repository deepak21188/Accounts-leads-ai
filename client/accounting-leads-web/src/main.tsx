import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { BrowserRouter } from 'react-router'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { ReactQueryDevtools } from '@tanstack/react-query-devtools'
import { MsalProvider } from '@azure/msal-react'
import { msalInstance } from '@/auth/msalInstance'
import App from './App.tsx'
import './index.css'

const queryClient = new QueryClient()

// msal-browser requires initialize() to resolve before the app renders or calls any other
// PublicClientApplication API. An IIFE (rather than top-level await) keeps the bundle
// compatible with Vite's default esbuild target.
void (async () => {
  await msalInstance.initialize()

  createRoot(document.getElementById('root')!).render(
    <StrictMode>
      <BrowserRouter>
        <MsalProvider instance={msalInstance}>
          <QueryClientProvider client={queryClient}>
            <App />
            <ReactQueryDevtools initialIsOpen={false} />
          </QueryClientProvider>
        </MsalProvider>
      </BrowserRouter>
    </StrictMode>,
  )
})()
