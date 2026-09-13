export type BrandTheme = {
  tenantCode: string
  applicationName: string
  logoUrl: string | null
  primaryColor: string
  secondaryColor: string
  accentColor: string
  navigationColor: string
  surfaceColor: string
  textColor: string
  fontFamily: string
  themeMode: 'light' | 'dark'
}

export const defaultTheme: BrandTheme = {
  tenantCode: 'UCREDIT',
  applicationName: 'uCredit',
  logoUrl: null,
  primaryColor: '#155EEF',
  secondaryColor: '#344054',
  accentColor: '#12B76A',
  navigationColor: '#101828',
  surfaceColor: '#FFFFFF',
  textColor: '#101828',
  fontFamily: 'Inter, system-ui, sans-serif',
  themeMode: 'light',
}

const hexColorPattern = /^#[0-9a-f]{6}$/i

export function normalizeTheme(theme: Partial<BrandTheme>): BrandTheme {
  const color = (value: string | undefined, fallback: string) =>
    value && hexColorPattern.test(value) ? value : fallback

  return {
    ...defaultTheme,
    ...theme,
    tenantCode: theme.tenantCode?.trim() || defaultTheme.tenantCode,
    applicationName: theme.applicationName?.trim() || defaultTheme.applicationName,
    primaryColor: color(theme.primaryColor, defaultTheme.primaryColor),
    secondaryColor: color(theme.secondaryColor, defaultTheme.secondaryColor),
    accentColor: color(theme.accentColor, defaultTheme.accentColor),
    navigationColor: color(theme.navigationColor, defaultTheme.navigationColor),
    surfaceColor: color(theme.surfaceColor, defaultTheme.surfaceColor),
    textColor: color(theme.textColor, defaultTheme.textColor),
    themeMode: theme.themeMode === 'dark' ? 'dark' : 'light',
  }
}

export function applyTheme(theme: BrandTheme): void {
  const root = document.documentElement
  root.style.setProperty('--color-primary', theme.primaryColor)
  root.style.setProperty('--color-secondary', theme.secondaryColor)
  root.style.setProperty('--color-accent', theme.accentColor)
  root.style.setProperty('--color-navigation', theme.navigationColor)
  root.style.setProperty('--color-surface', theme.surfaceColor)
  root.style.setProperty('--color-text', theme.textColor)
  root.style.setProperty('--font-family', theme.fontFamily)
  root.dataset.theme = theme.themeMode
  document.title = theme.applicationName
}

