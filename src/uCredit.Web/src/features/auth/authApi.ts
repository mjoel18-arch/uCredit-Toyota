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
export type ManagedCustomerEmail = { emailId: number; personId: number; contact: string | null; email: string; status: 'Active' | 'Inactive' | 'Unknown'; usageCodes: number[]; modifiedAt: string }
export type CustomerEmailUsage = { code: number; description: string }
export type CustomerEmailPayload = { contact?: string | null; email?: string | null; usageCodes: number[]; expectedModifiedAt?: string }
export type EmailStatePayload = { expectedModifiedAt: string }
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

export type ManagedCustomerAddress = {
  addressId: number
  personId: number
  postalCode: string | null
  state: string | null
  municipality: string | null
  city: string | null
  neighborhood: string | null
  streetAndNumber: string | null
  exteriorNumber: string | null
  interiorNumber: string | null
  addressTypeCode: number
  addressTypeDescription: string | null
  uses: string[]
  isActive: boolean
  isDefault: boolean
  modifiedAt: string
  countryCode: number
}

export type CustomerAddressPayload = {
  postalCode: string
  state: string
  municipality: string
  city: string
  neighborhood: string
  streetAndNumber: string
  exteriorNumber: string
  interiorNumber?: string
  reference?: string
  schedule?: string
  addressTypeCode: number
  uses: string[]
  isDefault: boolean
  expectedModifiedAt?: string
  countryCode: number
}

export type AddressStatePayload = { expectedModifiedAt: string; replacementAddressId?: number }

export type ManagedCustomerPhone = {
  phoneId: number
  personId: number
  phoneTypeCode: number
  longDistanceCode: string | null
  areaCode: string | null
  phoneNumber: string | null
  extension: string | null
  status: 'Active' | 'Inactive' | 'InheritedInactive' | 'Unknown'
  inactiveReason: string | null
  isDefault: boolean
  modifiedAt: string
  contactName: string | null
}

export type CustomerPhonePayload = {
  phoneTypeCode: number
  longDistanceCode?: string
  areaCode?: string
  phoneNumber: string
  extension?: string
  contactName?: string
  isDefault: boolean
  expectedModifiedAt?: string
}

export type PhoneStatePayload = { expectedModifiedAt: string; replacementPhoneId?: number }

export type ManagedCustomerAccount = {
  accountId: number
  personId: number
  bankId: number
  bankName: string | null
  branchNumber: number
  currencyCode: number
  currencyName: string | null
  accountTypeCode: number
  accountTypeName: string | null
  paymentMethodCode: number | null
  status: number
  maskedAccountNumber: string | null
  maskedClabe: string | null
  modifiedAt: string
}

export type CustomerAccountPayload = {
  bankId: number
  branchNumber: number
  currencyCode: number
  accountTypeCode: number
  accountNumber?: string | null
  clabe?: string | null
  expectedModifiedAt?: string
}
export type CustomerBank = { bankId: number; bankName: string }
export type AccountStatePayload = { expectedModifiedAt: string }

export function getCustomerAccounts(personId: number): Promise<ManagedCustomerAccount[]> {
  return apiRequest<ManagedCustomerAccount[]>(`/api/v1/customers/${encodeURIComponent(personId)}/accounts`)
}
export function getCustomerBanks(): Promise<CustomerBank[]> {
  return apiRequest<CustomerBank[]>('/api/v1/catalogs/banks')
}

export type CustomerRoleOption = { roleCode: number; roleName: string }

export function getCustomerRoleCatalog(): Promise<CustomerRoleOption[]> {
  return apiRequest<CustomerRoleOption[]>('/api/v1/catalogs/person-roles')
}
export function createCustomerAccount(personId: number, payload: CustomerAccountPayload): Promise<ManagedCustomerAccount> {
  return withCsrf<ManagedCustomerAccount>(`/api/v1/customers/${encodeURIComponent(personId)}/accounts`, { method: 'POST', body: JSON.stringify(payload) })
}
export function updateCustomerAccount(personId: number, accountId: number, payload: CustomerAccountPayload & { expectedModifiedAt: string }): Promise<ManagedCustomerAccount> {
  return withCsrf<ManagedCustomerAccount>(`/api/v1/customers/${encodeURIComponent(personId)}/accounts/${encodeURIComponent(accountId)}`, { method: 'PUT', body: JSON.stringify(payload) })
}
export function activateCustomerAccount(personId: number, accountId: number, payload: AccountStatePayload): Promise<ManagedCustomerAccount> {
  return withCsrf<ManagedCustomerAccount>(`/api/v1/customers/${encodeURIComponent(personId)}/accounts/${encodeURIComponent(accountId)}/activate`, { method: 'POST', body: JSON.stringify(payload) })
}
export function deactivateCustomerAccount(personId: number, accountId: number, payload: AccountStatePayload): Promise<ManagedCustomerAccount> {
  return withCsrf<ManagedCustomerAccount>(`/api/v1/customers/${encodeURIComponent(personId)}/accounts/${encodeURIComponent(accountId)}/deactivate`, { method: 'POST', body: JSON.stringify(payload) })
}

export function getCustomerPhones(personId: number): Promise<ManagedCustomerPhone[]> {
  return apiRequest<ManagedCustomerPhone[]>(`/api/v1/customers/${encodeURIComponent(personId)}/phones`)
}

export function getCustomerEmails(personId: number): Promise<ManagedCustomerEmail[]> {
  return apiRequest<ManagedCustomerEmail[]>(`/api/v1/customers/${encodeURIComponent(personId)}/emails`)
}
export function getCustomerEmailUsages(): Promise<CustomerEmailUsage[]> {
  return apiRequest<CustomerEmailUsage[]>('/api/v1/catalogs/email-uses')
}
export function createCustomerEmail(personId: number, payload: CustomerEmailPayload): Promise<ManagedCustomerEmail> {
  return withCsrf<ManagedCustomerEmail>(`/api/v1/customers/${encodeURIComponent(personId)}/emails`, { method: 'POST', body: JSON.stringify(payload) })
}
export function updateCustomerEmail(personId: number, emailId: number, payload: CustomerEmailPayload & { expectedModifiedAt: string }): Promise<ManagedCustomerEmail> {
  return withCsrf<ManagedCustomerEmail>(`/api/v1/customers/${encodeURIComponent(personId)}/emails/${encodeURIComponent(emailId)}`, { method: 'PUT', body: JSON.stringify(payload) })
}
export function activateCustomerEmail(personId: number, emailId: number, payload: EmailStatePayload): Promise<ManagedCustomerEmail> {
  return withCsrf<ManagedCustomerEmail>(`/api/v1/customers/${encodeURIComponent(personId)}/emails/${encodeURIComponent(emailId)}/activate`, { method: 'POST', body: JSON.stringify(payload) })
}
export function deactivateCustomerEmail(personId: number, emailId: number, payload: EmailStatePayload): Promise<ManagedCustomerEmail> {
  return withCsrf<ManagedCustomerEmail>(`/api/v1/customers/${encodeURIComponent(personId)}/emails/${encodeURIComponent(emailId)}/deactivate`, { method: 'POST', body: JSON.stringify(payload) })
}

export function createCustomerPhone(personId: number, payload: CustomerPhonePayload): Promise<ManagedCustomerPhone> {
  return withCsrf<ManagedCustomerPhone>(`/api/v1/customers/${encodeURIComponent(personId)}/phones`, { method: 'POST', body: JSON.stringify(payload) })
}

export function updateCustomerPhone(personId: number, phoneId: number, payload: CustomerPhonePayload & { expectedModifiedAt: string }): Promise<ManagedCustomerPhone> {
  return withCsrf<ManagedCustomerPhone>(`/api/v1/customers/${encodeURIComponent(personId)}/phones/${encodeURIComponent(phoneId)}`, { method: 'PUT', body: JSON.stringify(payload) })
}

export function activateCustomerPhone(personId: number, phoneId: number, payload: PhoneStatePayload): Promise<ManagedCustomerPhone> {
  return withCsrf<ManagedCustomerPhone>(`/api/v1/customers/${encodeURIComponent(personId)}/phones/${encodeURIComponent(phoneId)}/activate`, { method: 'POST', body: JSON.stringify(payload) })
}

export function deactivateCustomerPhone(personId: number, phoneId: number, payload: PhoneStatePayload): Promise<ManagedCustomerPhone> {
  return withCsrf<ManagedCustomerPhone>(`/api/v1/customers/${encodeURIComponent(personId)}/phones/${encodeURIComponent(phoneId)}/deactivate`, { method: 'POST', body: JSON.stringify(payload) })
}

export function getCustomerAddresses(personId: number): Promise<ManagedCustomerAddress[]> {
  return apiRequest<ManagedCustomerAddress[]>(`/api/v1/customers/${encodeURIComponent(personId)}/addresses`)
}

export function createCustomerAddress(personId: number, payload: CustomerAddressPayload): Promise<ManagedCustomerAddress> {
  return withCsrf<ManagedCustomerAddress>(`/api/v1/customers/${encodeURIComponent(personId)}/addresses`, { method: 'POST', body: JSON.stringify(payload) })
}

export function updateCustomerAddress(personId: number, addressId: number, payload: CustomerAddressPayload & { expectedModifiedAt: string }): Promise<ManagedCustomerAddress> {
  return withCsrf<ManagedCustomerAddress>(`/api/v1/customers/${encodeURIComponent(personId)}/addresses/${encodeURIComponent(addressId)}`, { method: 'PUT', body: JSON.stringify(payload) })
}

export function activateCustomerAddress(personId: number, addressId: number, payload: AddressStatePayload): Promise<ManagedCustomerAddress> {
  return withCsrf<ManagedCustomerAddress>(`/api/v1/customers/${encodeURIComponent(personId)}/addresses/${encodeURIComponent(addressId)}/activate`, { method: 'POST', body: JSON.stringify(payload) })
}

export function deactivateCustomerAddress(personId: number, addressId: number, payload: AddressStatePayload): Promise<ManagedCustomerAddress> {
  return withCsrf<ManagedCustomerAddress>(`/api/v1/customers/${encodeURIComponent(personId)}/addresses/${encodeURIComponent(addressId)}/deactivate`, { method: 'POST', body: JSON.stringify(payload) })
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

export type CustomerProfileRequirement = 'generalData' | 'address' | 'phone' | 'account'
export type CustomerProfileReadiness = {
  personId: number
  hasGeneralData: boolean
  hasAddress: boolean
  hasPhone: boolean
  hasAccount: boolean
  canCreateContract: boolean
  missingRequirements: CustomerProfileRequirement[]
}

export function getCustomerProfileReadiness(personId: number): Promise<CustomerProfileReadiness> {
  return apiRequest<CustomerProfileReadiness>('/api/v1/customers/' + encodeURIComponent(personId) + '/readiness')
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
  taxRegimeCode: string
  roleCodes: number[]
  pepConfirmed: boolean
}

export function createCustomer(payload: CustomerCreatePayload): Promise<{ personId: number; pepValidationStatus: 'Executed' | 'NotExecuted' }> {
  return withCsrf('/api/v1/customers', { method: 'POST', body: JSON.stringify(payload) })
}
