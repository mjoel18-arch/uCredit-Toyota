import { apiRequest } from '../../shared/api/apiClient'

export type AuthTenant = {
  tenantId: string
  tenantCode: string
}

export type AuthMembership = {
  tenantId: string
  tenantCode: string
  tenantName: string
  permissions: string[]
  allowedCompanyIds: number[]
}

export type AuthSession = {
  userName: string
  tenant: AuthTenant | null
  permissions: string[]
  memberships: AuthMembership[]
}

type CsrfResponse = {
  requestToken: string
  headerName: string
}

async function withCsrf<T>(path: string, init: RequestInit = {}): Promise<T> {
  const csrf = await apiRequest<CsrfResponse>('/api/v1/auth/csrf')
  const headers = new Headers(init.headers)
  headers.set(csrf.headerName, csrf.requestToken)
  return apiRequest<T>(path, { ...init, headers })
}

export function getSession(): Promise<AuthSession> {
  return apiRequest<AuthSession>('/api/v1/auth/me')
}

export async function login(userName: string, password: string): Promise<void> {
  await withCsrf('/api/v1/auth/login', {
    method: 'POST',
    body: JSON.stringify({ userName, password }),
  })
}

export async function selectTenant(tenantCode: string): Promise<void> {
  await withCsrf('/api/v1/auth/select-tenant', {
    method: 'POST',
    body: JSON.stringify({ tenantCode }),
  })
}

export async function logout(): Promise<void> {
  await withCsrf<void>('/api/v1/auth/logout', { method: 'POST' })
}

export type SafeContract = {
  contractNumber: string
  statusName?: string | null
  operationTypeName?: string | null
}

export type ContractDetail = {
  contractNumber: string
  status: { code: number | null; description: string | null } | null
  operationType: { code: string | null; description: string | null } | null
  customerName: string | null
  currencyCode: string | null
  currencyName: string | null
  financedAmount: number | null
  outstandingBalance: number | null
  currentTerm: number
  originalTerm: number | null
  startDate: string
  activationDate: string
  disbursementDate: string | null
  firstPaymentDate: string | null
  lastPaymentDate: string | null
}

type ContractSearchResponse = {
  items: SafeContract[]
  page: number
  pageSize: number
  total: number
}

export function getContractByNumber(contractNumber: string): Promise<ContractDetail> {
  return apiRequest<ContractDetail>('/api/v1/contracts/' + encodeURIComponent(contractNumber))
}

export function searchContracts(contractNumber: string): Promise<ContractSearchResponse> {
  const query = new URLSearchParams({
    contractNumber,
    page: '1',
    pageSize: '10',
  })
  return apiRequest<ContractSearchResponse>('/api/v1/contracts?' + query.toString())
}