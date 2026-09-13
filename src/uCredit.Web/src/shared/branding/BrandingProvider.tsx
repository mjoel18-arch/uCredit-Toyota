import { createContext, useContext, useEffect, useMemo, useState, type PropsWithChildren } from 'react'
import { applyTheme, defaultTheme, normalizeTheme, type BrandTheme } from './brandTheme'

type BrandingContextValue = {
  theme: BrandTheme
  loading: boolean
}

const BrandingContext = createContext<BrandingContextValue>({
  theme: defaultTheme,
  loading: true,
})

export function BrandingProvider({ children }: PropsWithChildren) {
  const [theme, setTheme] = useState(defaultTheme)
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    const controller = new AbortController()

    async function loadBranding() {
      try {
        const response = await fetch('/api/v1/branding/current', {
          signal: controller.signal,
          headers: { 'X-Tenant-Code': 'DEMO' },
        })

        if (!response.ok) throw new Error('Branding is unavailable')

        const loaded = normalizeTheme((await response.json()) as Partial<BrandTheme>)
        applyTheme(loaded)
        setTheme(loaded)
      } catch (error) {
        if ((error as Error).name !== 'AbortError') {
          applyTheme(defaultTheme)
          setTheme(defaultTheme)
        }
      } finally {
        setLoading(false)
      }
    }

    void loadBranding()
    return () => controller.abort()
  }, [])

  const value = useMemo(() => ({ theme, loading }), [theme, loading])
  return <BrandingContext.Provider value={value}>{children}</BrandingContext.Provider>
}

export const useBranding = () => useContext(BrandingContext)

