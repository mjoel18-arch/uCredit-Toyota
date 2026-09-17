export type BrandTheme = {
  tenantCode: string
  productName: string
  customerName: string
  logoUrl: string | null
  primaryColor: string
  secondaryColor: string
  accentColor: string
  navigationColor: string
  surfaceColor: string
  textColor: string
  fontFamily: string
  themeMode: 'light' | 'dark'
  browserTitle: string
  faviconUrl: string | null
}

export const defaultTheme: BrandTheme = {
  tenantCode: 'UCREDIT',
  productName: 'uCredit',
  customerName: '',
  logoUrl: null,
  primaryColor: '#155EEF',
  secondaryColor: '#344054',
  accentColor: '#12B76A',
  navigationColor: '#101828',
  surfaceColor: '#FFFFFF',
  textColor: '#101828',
  fontFamily: 'Inter, system-ui, sans-serif',
  themeMode: 'light',
  browserTitle: 'uCredit',
  faviconUrl: null,
}

export const demoInitialTheme: BrandTheme = {
  ...defaultTheme,
  tenantCode: 'TOYOTA',
  productName: 'uCredit-auto',
  customerName: 'Toyota Financial Services',
  logoUrl: '/branding/toyota/logo.png',
  primaryColor: '#EB0A1E',
  secondaryColor: '#1D1D1F',
  accentColor: '#EB0A1E',
  navigationColor: '#1D1D1F',
  browserTitle: 'uCredit-auto | Toyota Financial Services',
}

const hexColorPattern = /^#[0-9a-f]{6}$/i

export function normalizeTheme(theme: Partial<BrandTheme>): BrandTheme {
  const color = (value: string | undefined, fallback: string) =>
    value && hexColorPattern.test(value) ? value : fallback
  const localAsset = (value: string | null | undefined, fallback: string | null) =>
    value && value.startsWith('/branding/') && !value.startsWith('//') ? value : fallback

  return {
    ...defaultTheme,
    ...theme,
    tenantCode: theme.tenantCode?.trim() || defaultTheme.tenantCode,
    productName: theme.productName?.trim() || defaultTheme.productName,
    customerName: theme.customerName?.trim() || defaultTheme.customerName,
    logoUrl: localAsset(theme.logoUrl, defaultTheme.logoUrl),
    primaryColor: color(theme.primaryColor, defaultTheme.primaryColor),
    secondaryColor: color(theme.secondaryColor, defaultTheme.secondaryColor),
    accentColor: color(theme.accentColor, defaultTheme.accentColor),
    navigationColor: color(theme.navigationColor, defaultTheme.navigationColor),
    surfaceColor: color(theme.surfaceColor, defaultTheme.surfaceColor),
    textColor: color(theme.textColor, defaultTheme.textColor),
    themeMode: theme.themeMode === 'dark' ? 'dark' : 'light',
    browserTitle: theme.browserTitle?.trim() || defaultTheme.browserTitle,
    faviconUrl: localAsset(theme.faviconUrl, defaultTheme.faviconUrl),
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
  document.title = theme.browserTitle

  const favicon = document.querySelector<HTMLLinkElement>('link[rel="icon"]')
  if (theme.faviconUrl) {
    const link = favicon ?? document.createElement('link')
    link.rel = 'icon'
    link.href = theme.faviconUrl
    if (!favicon) document.head.appendChild(link)
  } else if (favicon) {
    favicon.removeAttribute('href')
  }
}

