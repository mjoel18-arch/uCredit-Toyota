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

export async function loadBranding(
  fetcher: typeof fetch = fetch,
  signal?: AbortSignal,
): Promise<BrandTheme> {
  const response = await fetcher('/api/v1/branding/current', {
    signal,
    credentials: 'include',
  })

  if (!response.ok) throw new Error('Branding is unavailable')

  return normalizeTheme((await response.json()) as Partial<BrandTheme>)
}

export async function loadAndApplyBranding(
  fetcher: typeof fetch = fetch,
  signal?: AbortSignal,
): Promise<BrandTheme> {
  try {
    const loaded = await loadBranding(fetcher, signal)
    applyTheme(loaded)
    return loaded
  } catch (error) {
    if ((error as Error).name === 'AbortError') throw error

    applyTheme(defaultTheme)
    return defaultTheme
  }
}

export function BrandingProvider({ children }: PropsWithChildren) {
  const [theme, setTheme] = useState(defaultTheme)
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    const controller = new AbortController()

    async function refreshBranding() {
      try {
        const loaded = await loadAndApplyBranding(fetch, controller.signal)
        setTheme(loaded)
      } catch (error) {
        if ((error as Error).name !== 'AbortError') {
          setTheme(defaultTheme)
        }
      } finally {
        setLoading(false)
      }
    }

    void refreshBranding()
    return () => controller.abort()
  }, [])

  const value = useMemo(() => ({ theme, loading }), [theme, loading])
  return <BrandingContext.Provider value={value}>{children}</BrandingContext.Provider>
}

export const useBranding = () => useContext(BrandingContext)

