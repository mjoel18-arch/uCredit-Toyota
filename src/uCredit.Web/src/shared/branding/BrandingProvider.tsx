import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState, type PropsWithChildren } from 'react'
import { applyTheme, defaultTheme, demoInitialTheme, normalizeTheme, type BrandTheme } from './brandTheme'

type BrandingContextValue = {
  theme: BrandTheme
  loading: boolean
  refresh: () => Promise<void>
  reset: () => void
}

const BrandingContext = createContext<BrandingContextValue>({
  theme: defaultTheme,
  loading: true,
  refresh: async () => undefined,
  reset: () => undefined,
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
  const [theme, setTheme] = useState(demoInitialTheme)
  const [loading, setLoading] = useState(true)
  const requestVersion = useRef(0)

  const refresh = useCallback(async () => {
    const version = ++requestVersion.current
    setLoading(true)
    try {
      const loaded = await loadBranding()
      if (version !== requestVersion.current) return
      applyTheme(loaded)
      setTheme(loaded)
    } catch {
      if (version !== requestVersion.current) return
      applyTheme(demoInitialTheme)
      setTheme(demoInitialTheme)
    } finally {
      if (version === requestVersion.current) setLoading(false)
    }
  }, [])

  const reset = useCallback(() => {
    requestVersion.current += 1
    applyTheme(demoInitialTheme)
    setTheme(demoInitialTheme)
    setLoading(false)
  }, [])

  useEffect(() => {
    const controller = new AbortController()
    const version = ++requestVersion.current
    applyTheme(demoInitialTheme)
    void loadBranding(fetch, controller.signal).then((loaded) => {
      if (version !== requestVersion.current) return
      if (loaded.tenantCode === defaultTheme.tenantCode) return
      applyTheme(loaded)
      setTheme(loaded)
    }).catch((error: unknown) => {
      if (version === requestVersion.current && (error as Error).name !== 'AbortError') {
        applyTheme(demoInitialTheme)
        setTheme(demoInitialTheme)
      }
    }).finally(() => {
      if (version === requestVersion.current) setLoading(false)
    })
    return () => controller.abort()
  }, [])

  const value = useMemo(() => ({ theme, loading, refresh, reset }), [theme, loading, refresh, reset])
  return <BrandingContext.Provider value={value}>{children}</BrandingContext.Provider>
}

export const useBranding = () => useContext(BrandingContext)

