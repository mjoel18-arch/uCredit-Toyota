import { beforeEach, describe, expect, it, vi } from 'vitest'
import { defaultTheme } from '../../src/shared/branding/brandTheme'
import { loadAndApplyBranding, loadBranding } from '../../src/shared/branding/BrandingProvider'

const fetchMock = vi.fn<(input: RequestInfo | URL, init?: RequestInit) => Promise<Response>>()
const setProperty = vi.fn()
const removeAttribute = vi.fn()

function response(status: number, body?: unknown): Response {
  return body === undefined
    ? new Response(null, { status })
    : new Response(JSON.stringify(body), {
      status,
      headers: { 'Content-Type': 'application/json' },
    })
}

beforeEach(() => {
  fetchMock.mockReset()
  setProperty.mockReset()
  removeAttribute.mockReset()
  vi.stubGlobal('fetch', fetchMock)
  vi.stubGlobal('document', {
    documentElement: { style: { setProperty }, dataset: {} },
    querySelector: vi.fn(() => null),
    createElement: vi.fn(() => ({ rel: '', href: '' })),
    head: { appendChild: vi.fn() },
    title: '',
  })
})

describe('dynamic branding provider', () => {
  it('requests branding with the signed identity cookie and no tenant header', async () => {
    fetchMock.mockResolvedValueOnce(response(200, { tenantCode: 'ubimia-dev' }))

    await loadBranding(fetchMock)

    expect(fetchMock).toHaveBeenCalledWith('/api/v1/branding/current', expect.objectContaining({ credentials: 'include' }))
    expect(new Headers(fetchMock.mock.calls[0]?.[1]?.headers).has('X-Tenant-Code')).toBe(false)
  })

  it('normalizes and applies the received theme', async () => {
    fetchMock.mockResolvedValueOnce(response(200, {
      tenantCode: 'ubimia-dev',
      productName: 'Ubimia Crédito',
      customerName: 'Ubimia',
      primaryColor: '#123456',
      browserTitle: 'Ubimia Crédito | Ubimia',
      themeMode: 'dark',
    }))

    const theme = await loadAndApplyBranding(fetchMock)

    expect(theme.productName).toBe('Ubimia Crédito')
    expect(theme.customerName).toBe('Ubimia')
    expect(theme.primaryColor).toBe('#123456')
    expect(document.documentElement.style.setProperty).toHaveBeenCalledWith('--color-primary', '#123456')
    expect(document.documentElement.dataset.theme).toBe('dark')
    expect(document.title).toBe('Ubimia Crédito | Ubimia')
  })

  it('keeps the Toyota official PNG route and browser title', async () => {
    fetchMock.mockResolvedValueOnce(response(200, {
      tenantCode: 'TOYOTA',
      productName: 'uCredit-auto',
      customerName: 'Toyota Financial Services',
      logoUrl: '/branding/toyota/logo.png',
      browserTitle: 'uCredit-auto | Toyota Financial Services',
    }))

    const theme = await loadAndApplyBranding(fetchMock)

    expect(theme.logoUrl).toBe('/branding/toyota/logo.png')
    expect(theme.customerName).toBe('Toyota Financial Services')
    expect(document.title).toBe('uCredit-auto | Toyota Financial Services')
  })

  it('falls back to the safe default theme when branding fails', async () => {
    fetchMock.mockResolvedValueOnce(response(500))

    await expect(loadAndApplyBranding(fetchMock)).resolves.toEqual(defaultTheme)

    expect(document.documentElement.style.setProperty).toHaveBeenCalledWith('--color-primary', defaultTheme.primaryColor)
    expect(document.documentElement.dataset.theme).toBe(defaultTheme.themeMode)
    expect(document.title).toBe(defaultTheme.browserTitle)
    expect(defaultTheme.logoUrl).toBeNull()
  })

  it('rejects non-local logo and favicon assets', async () => {
    fetchMock.mockResolvedValueOnce(response(200, {
      logoUrl: 'https://example.test/logo.svg',
      faviconUrl: '//example.test/favicon.ico',
    }))

    const theme = await loadBranding(fetchMock)

    expect(theme.logoUrl).toBeNull()
    expect(theme.faviconUrl).toBeNull()
  })

  it('does not persist branding data or sensitive session material', async () => {
    const setItem = vi.fn()
    vi.stubGlobal('localStorage', { setItem })
    vi.stubGlobal('sessionStorage', { setItem })
    fetchMock.mockResolvedValueOnce(response(200, { productName: 'Ubimia Crédito' }))

    await loadAndApplyBranding(fetchMock)

    expect(setItem).not.toHaveBeenCalled()
  })
})
