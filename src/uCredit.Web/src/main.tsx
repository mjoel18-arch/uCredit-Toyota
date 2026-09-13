import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { App } from './App'
import { BrandingProvider } from './shared/branding/BrandingProvider'
import './styles.css'

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <BrandingProvider>
      <App />
    </BrandingProvider>
  </StrictMode>,
)

