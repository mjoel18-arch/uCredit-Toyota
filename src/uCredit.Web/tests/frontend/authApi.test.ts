import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ApiError, apiRequest } from '../../src/shared/api/apiClient'
import {
  getContractByNumber,
  getSession,
  login,
  logout,
  searchContracts,
  selectTenant,
} from '../../src/features/auth/authApi'

const fetchMock = vi.fn<(input: RequestInfo | URL, init?: RequestInit) => Promise<Response>>()

function response(status: number, body?: unknown): Response {
  return body === undefined
    ? new Response(null, { status })
    : new Response(JSON.stringify(body), {
      status,
      headers: { 'Content-Type': 'application/json' },
    })
}

function csrfResponse() {
  return response(200, { requestToken: 'csrf-test-token', headerName: 'X-CSRF-TOKEN' })
}

beforeEach(() => {
  fetchMock.mockReset()
  vi.stubGlobal('fetch', fetchMock)
})

describe('local Identity API client', () => {
  it('loads the current session and always includes browser credentials', async () => {
    fetchMock.mockResolvedValueOnce(response(200, {
      userName: 'admin@example.test',
      tenant: null,
      permissions: [],
      memberships: [{ tenantId: 'tenant-1', tenantCode: 'ubimia-dev', tenantName: 'Ubimia', permissions: ['contracts.read'], allowedCompanyIds: [1] }],
    }))

    const session = await getSession()

    expect(session.tenant).toBeNull()
    expect(session.memberships[0]?.tenantCode).toBe('ubimia-dev')
    expect(fetchMock.mock.calls[0]?.[1]).toMatchObject({ credentials: 'include' })
  })

  it('returns the same typed failure for invalid credentials without exposing response content', async () => {
    fetchMock.mockResolvedValueOnce(csrfResponse()).mockResolvedValueOnce(response(401, {
      password: 'must-not-be-read',
      token: 'must-not-be-read',
    }))

    await expect(login('admin@example.test', 'wrong-password')).rejects.toMatchObject({ status: 401 })
    const loginInit = fetchMock.mock.calls[1]?.[1]
    expect(loginInit).toMatchObject({ credentials: 'include' })
    expect(new Headers(loginInit?.headers).get('X-CSRF-TOKEN')).toBe('csrf-test-token')
  })

  it('does not persist password, CSRF token, cookies, or API responses in browser storage', async () => {
    const setItem = vi.fn()
    vi.stubGlobal('localStorage', { setItem })
    vi.stubGlobal('sessionStorage', { setItem })
    fetchMock.mockResolvedValueOnce(csrfResponse()).mockResolvedValueOnce(response(200, { authenticated: true }))

    await login('admin@example.test', 'temporary-password')

    expect(setItem).not.toHaveBeenCalled()
  })

  it('selects a tenant with a fresh CSRF token and then can load the selected session', async () => {
    fetchMock
      .mockResolvedValueOnce(csrfResponse())
      .mockResolvedValueOnce(response(200, { tenantCode: 'ubimia-dev', permissions: ['contracts.read'] }))
      .mockResolvedValueOnce(response(200, {
        userName: 'admin@example.test',
        tenant: { tenantId: 'tenant-1', tenantCode: 'ubimia-dev' },
        permissions: ['contracts.read'],
        memberships: [],
      }))

    await selectTenant('ubimia-dev')
    const session = await getSession()

    expect(session.tenant?.tenantCode).toBe('ubimia-dev')
    const selectInit = fetchMock.mock.calls[1]?.[1]
    expect(selectInit).toMatchObject({
      method: 'POST',
      credentials: 'include',
      body: JSON.stringify({ tenantCode: 'ubimia-dev' }),
    })
    expect(new Headers(selectInit?.headers).get('X-CSRF-TOKEN')).toBe('csrf-test-token')
  })

  it('logs out with antiforgery and accepts a 204 idempotent response', async () => {
    fetchMock.mockResolvedValueOnce(csrfResponse()).mockResolvedValueOnce(response(204))

    await expect(logout()).resolves.toBeUndefined()
    const logoutInit = fetchMock.mock.calls[1]?.[1]
    expect(logoutInit).toMatchObject({ method: 'POST', credentials: 'include' })
    expect(new Headers(logoutInit?.headers).get('X-CSRF-TOKEN')).toBe('csrf-test-token')
  })

  it('propagates 401 and 403 without redirect handling', async () => {
    fetchMock.mockResolvedValueOnce(response(401))
    await expect(getContractByNumber('ABC/123')).rejects.toEqual(expect.any(ApiError))
    expect(fetchMock.mock.calls[0]?.[0]).toBe('/api/v1/contracts/ABC%2F123')

    fetchMock.mockReset()
    vi.stubGlobal('fetch', fetchMock)
    fetchMock.mockResolvedValueOnce(response(403))
    await expect(searchContracts('ABC/123')).rejects.toMatchObject({ status: 403 })
    expect(fetchMock.mock.calls[0]?.[0]).toBe('/api/v1/contracts?contractNumber=ABC%2F123&page=1&pageSize=10')
  })

  it('sends no request body for the CSRF-free GET session request', async () => {
    fetchMock.mockResolvedValueOnce(response(200, {
      userName: 'admin@example.test',
      tenant: null,
      permissions: [],
      memberships: [],
    }))

    await apiRequest('/api/v1/auth/me')
    expect(fetchMock.mock.calls[0]?.[1]).toMatchObject({ credentials: 'include' })
    expect(fetchMock.mock.calls[0]?.[1]?.body).toBeUndefined()
  })
})