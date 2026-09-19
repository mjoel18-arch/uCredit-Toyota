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


export type ContractAmortizationPayment = {
  paymentNumber: number
  status: 'Generated' | 'Pending'
  startDate: string
  endDate: string
  dueDate: string
  calculationBase: number
  outstandingBalance: number
  amortization: number
  interest: number
  iva: number
  payment: number
  paymentWithIva: number
  totalPayment: number
}

export type ContractAmortization = {
  contractNumber: string
  financingType: number
  version: number
  downPayment: ContractAmortizationPayment | null
  payments: ContractAmortizationPayment[]
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


export function getContractAmortization(contractNumber: string): Promise<ContractAmortization> {
  return apiRequest<ContractAmortization>(
    '/api/v1/contracts/' + encodeURIComponent(contractNumber) + '/amortization-schedule',
  )
}
export function searchContracts(contractNumber: string): Promise<ContractSearchResponse> {
  const query = new URLSearchParams({
    contractNumber,
    page: '1',
    pageSize: '10',
  })
  return apiRequest<ContractSearchResponse>('/api/v1/contracts?' + query.toString())
}

export type CustomerAddress = {
  addressId: number
  postalCode: string | null
  state: string | null
  municipality: string | null
  city: string | null
  neighborhood: string | null
  streetAndNumber: string | null
  exteriorNumber: string | null
  interiorNumber: string | null
  typeCode: number
  typeDescription: string | null
}

export type CustomerPhone = {
  phoneId: number
  areaCode: string | null
  phoneNumber: string | null
  extension: string | null
  isDefault: boolean
}

export type CustomerEmail = { emailId: number; contact: string | null; email: string | null }
export type CustomerRole = { code: number; description: string | null }
export type CustomerListItem = {
  personId: number
  rfcMasked: string | null
  name: string
  legalPersonality: { code: number; description: string | null }
  status: { code: number; description: string | null }
  primaryAddress: CustomerAddress | null
  primaryPhone: CustomerPhone | null
  roles: CustomerRole[]
}
export type CustomerPage = { items: CustomerListItem[]; page: number; pageSize: number; total: number }
export type CustomerDetail = Omit<CustomerListItem, 'rfcMasked'> & {
  rfc: string | null
  activePhones: CustomerPhone[]
  activeEmails: CustomerEmail[]
}

export function searchCustomers(criteria: { personId?: number; rfc?: string; name?: string; legalPersonality?: number; page?: number; pageSize?: number }): Promise<CustomerPage> {
  const query = new URLSearchParams()
  if (criteria.personId) query.set('personId', String(criteria.personId))
  if (criteria.rfc) query.set('rfc', criteria.rfc)
  if (criteria.name) query.set('name', criteria.name)
  if (criteria.legalPersonality) query.set('legalPersonality', String(criteria.legalPersonality))
  query.set('page', String(criteria.page ?? 1))
  query.set('pageSize', String(criteria.pageSize ?? 20))
  return apiRequest<CustomerPage>('/api/v1/customers/?' + query.toString())
}

export function getCustomerByPersonId(personId: number): Promise<CustomerDetail> {
  return apiRequest<CustomerDetail>('/api/v1/customers/' + encodeURIComponent(personId))
}

export type CustomerCreatePayload = {
  legalPersonality: number
  rfc: string
  firstName?: string
  paternalSurname?: string
  maternalSurname?: string
  legalName?: string
  capitalRegime?: string
  constitutionOrBirthDate: string
  countryCode: number
  groupCode: number
  riskCode: number
  contactFormCode: number
  taxRegimeCode: number
  addressTypeCode: number
  postalCode: string
  state: string
  city: string
  municipality: string
  neighborhood: string
  streetAndNumber: string
  exteriorNumber: string
  interiorNumber?: string
  addressReference?: string
  addressSchedule?: string
  addressStatusCode: number
  phoneTypeCode: number
  areaCode: string
  phoneNumber: string
  phoneExtension?: string
  phoneContact?: string
  emailContact: string
  email: string
  emailUsageCodes: number[]
  pepConfirmed: boolean
}

export function createCustomer(payload: CustomerCreatePayload): Promise<{ personId: number; pepValidationStatus: 'Executed' | 'NotExecuted' }> {
  return withCsrf('/api/v1/customers', { method: 'POST', body: JSON.stringify(payload) })
}
