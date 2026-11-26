import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import '@fontsource-variable/inter'
import './index.css'
import { AppProviders } from '@/app/providers'
import { AppRouter } from '@/app/router'
import { consumeHandoff } from '@/lib/session-handoff'

// Restore a session handed over from another workspace origin before React
// mounts, so the first render already sees it.
consumeHandoff()

document.getElementById('boot')?.remove()

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <AppProviders>
      <AppRouter />
    </AppProviders>
  </StrictMode>,
)
