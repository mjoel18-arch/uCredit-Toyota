import React from 'react'
import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { ApiError } from '../../src/shared/api/apiClient'
import { activateCustomerAccount, activateCustomerEmail, activateCustomerPhone, createCustomerAccount, createCustomerEmail, createCustomerPhone, deactivateCustomerAccount, deactivateCustomerEmail, deactivateCustomerPhone, getCustomerAccounts, getCustomerAddresses, getCustomerBanks, getCustomerByPersonId, getCustomerEmailUsages, getCustomerEmails, getCustomerGeneral, getCustomerPhones, getCustomerProfileReadiness, searchCustomers, updateCustomerAccount, updateCustomerEmail, updateCustomerGeneral, updateCustomerPhone } from '../../src/features/auth/authApi'
import { CustomersView } from '../../src/features/customers/CustomersView'

vi.mock('../../src/features/auth/authApi', async () => {
  const actual = await vi.importActual<typeof import('../../src/features/auth/authApi')>('../../src/features/auth/authApi')
  return { ...actual, activateCustomerAccount: vi.fn(), activateCustomerEmail: vi.fn(), activateCustomerPhone: vi.fn(), createCustomerAccount: vi.fn(), createCustomerEmail: vi.fn(), createCustomerPhone: vi.fn(), deactivateCustomerAccount: vi.fn(), deactivateCustomerEmail: vi.fn(), deactivateCustomerPhone: vi.fn(), getCustomerAccounts: vi.fn(), getCustomerAddresses: vi.fn(), getCustomerBanks: vi.fn(), getCustomerByPersonId: vi.fn(), getCustomerEmailUsages: vi.fn(), getCustomerEmails: vi.fn(), getCustomerGeneral: vi.fn(), getCustomerPhones: vi.fn(), getCustomerProfileReadiness: vi.fn(), searchCustomers: vi.fn(), updateCustomerAccount: vi.fn(), updateCustomerEmail: vi.fn(), updateCustomerGeneral: vi.fn(), updateCustomerPhone: vi.fn() }
})

const searchMock = vi.mocked(searchCustomers)
const detailMock = vi.mocked(getCustomerByPersonId)
const readinessMock = vi.mocked(getCustomerProfileReadiness)
const addressesMock = vi.mocked(getCustomerAddresses)
const phonesMock = vi.mocked(getCustomerPhones)
const accountsMock = vi.mocked(getCustomerAccounts)
const banksMock = vi.mocked(getCustomerBanks)
const generalMock = vi.mocked(getCustomerGeneral)
const updateGeneralMock = vi.mocked(updateCustomerGeneral)
const createAccountMock = vi.mocked(createCustomerAccount)
const updateAccountMock = vi.mocked(updateCustomerAccount)
const activateAccountMock = vi.mocked(activateCustomerAccount)
const deactivateAccountMock = vi.mocked(deactivateCustomerAccount)
const emailsMock = vi.mocked(getCustomerEmails)
const emailUsagesMock = vi.mocked(getCustomerEmailUsages)
const createEmailMock = vi.mocked(createCustomerEmail)
const updateEmailMock = vi.mocked(updateCustomerEmail)
const activateEmailMock = vi.mocked(activateCustomerEmail)
const deactivateEmailMock = vi.mocked(deactivateCustomerEmail)
const createPhoneMock = vi.mocked(createCustomerPhone)
const updatePhoneMock = vi.mocked(updateCustomerPhone)
const activatePhoneMock = vi.mocked(activateCustomerPhone)
const deactivatePhoneMock = vi.mocked(deactivateCustomerPhone)
const unauthorized = vi.fn()

beforeEach(() => { searchMock.mockReset(); detailMock.mockReset(); readinessMock.mockReset(); addressesMock.mockReset(); phonesMock.mockReset(); accountsMock.mockReset(); banksMock.mockReset(); generalMock.mockReset(); updateGeneralMock.mockReset(); createAccountMock.mockReset(); updateAccountMock.mockReset(); activateAccountMock.mockReset(); deactivateAccountMock.mockReset(); createPhoneMock.mockReset(); updatePhoneMock.mockReset(); activatePhoneMock.mockReset(); deactivatePhoneMock.mockReset(); emailsMock.mockReset(); emailUsagesMock.mockReset(); createEmailMock.mockReset(); updateEmailMock.mockReset(); activateEmailMock.mockReset(); deactivateEmailMock.mockReset(); addressesMock.mockResolvedValue([]); phonesMock.mockResolvedValue([]); accountsMock.mockResolvedValue([]); emailsMock.mockResolvedValue([]); emailUsagesMock.mockResolvedValue([{ code: 1, description: 'Envío de facturas' }, { code: 2, description: 'Envío de estado de cuenta' }, { code: 3, description: 'Salesforce' }]); banksMock.mockResolvedValue([{ bankId: 2, bankName: 'Banco Alfa' }, { bankId: 4, bankName: 'Banco Beta' }]); generalMock.mockResolvedValue({ personId: 42, legalPersonalityCode: 1, rfc: 'ABC010203AB1', firstName: 'Nombre', paternalSurname: 'Apellido', maternalSurname: null, birthDate: '1980-01-01T00:00:00Z', legalName: null, contactName: null, contactPosition: null, statusCode: 1, personModifiedAt: '2025-01-01T00:00:00Z', subtypeModifiedAt: '2025-01-02T00:00:00Z', roles: [{ roleCode: 1, roleName: 'CLIENTE', isActive: true, isEditable: true }, { roleCode: 2, roleName: 'Histórico', isActive: false, isEditable: false }] }); updateGeneralMock.mockResolvedValue({} as never); createAccountMock.mockResolvedValue({} as never); updateAccountMock.mockResolvedValue({} as never); activateAccountMock.mockResolvedValue({} as never); deactivateAccountMock.mockResolvedValue({} as never); createPhoneMock.mockResolvedValue({} as never); updatePhoneMock.mockResolvedValue({} as never); activatePhoneMock.mockResolvedValue({} as never); deactivatePhoneMock.mockResolvedValue({} as never); createEmailMock.mockResolvedValue({} as never); updateEmailMock.mockResolvedValue({} as never); activateEmailMock.mockResolvedValue({} as never); deactivateEmailMock.mockResolvedValue({} as never); unauthorized.mockReset() })
afterEach(() => cleanup())

const accountFixture = { accountId: 7001, personId: 42, bankId: 2, bankName: 'Banco de prueba', branchNumber: 12, currencyCode: 1, currencyName: 'Pesos', accountTypeCode: 1, accountTypeName: 'Cheques', paymentMethodCode: null, status: 1, maskedAccountNumber: '••••0001', maskedClabe: '••••0002', modifiedAt: '2025-01-01T00:00:00Z' }

function configureAccountDetail(accounts = [accountFixture], canCreate = true) {
  searchMock.mockResolvedValue({ items: [{ personId: 42, rfcMasked: 'ABC0******B1', name: 'Cliente', legalPersonality: { code: 1, description: 'FISICA' }, status: { code: 1, description: 'ACTIVO' }, primaryAddress: null, primaryPhone: null, roles: [] }], page: 1, pageSize: 20, total: 1 })
  detailMock.mockResolvedValue({ personId: 42, rfc: null, name: 'Cliente', legalPersonality: { code: 1, description: 'FISICA' }, status: { code: 1, description: 'ACTIVO' }, primaryAddress: null, primaryPhone: null, activePhones: [], activeEmails: [], roles: [] })
  readinessMock.mockResolvedValue({ personId: 42, hasGeneralData: true, hasAddress: true, hasPhone: true, hasAccount: canCreate, canCreateContract: canCreate, missingRequirements: canCreate ? [] : ['account'] })
  accountsMock.mockResolvedValue(accounts)
}

function accountSection() {
  return screen.getByRole('heading', { name: 'Cuentas' }).closest('section') as HTMLElement
}

async function openAccountDetail(canWrite = true) {
  render(<CustomersView productName="uCredit-auto" customerName="Toyota Financial Services" onUnauthorized={unauthorized} canCreate={canWrite} />)
  fireEvent.change(screen.getByLabelText('Identificador'), { target: { value: '42' } })
  fireEvent.click(screen.getByRole('button', { name: 'Buscar clientes' }))
  fireEvent.click(await screen.findByRole('button', { name: 'Ver detalle' }))
  await screen.findByRole('heading', { name: 'Cuentas' })
}

describe('CustomersView', () => {
  it('requires a search criterion and renders paginated masked results', async () => {
    render(<CustomersView productName="uCredit-auto" customerName="Toyota Financial Services" onUnauthorized={unauthorized} />)
    fireEvent.click(screen.getByRole('button', { name: 'Buscar clientes' }))
    expect(screen.getByRole('alert').textContent).toContain('Captura un identificador')

    searchMock.mockResolvedValue({ items: [{ personId: 42, rfcMasked: 'ABC0******B1', name: 'Cliente', legalPersonality: { code: 1, description: 'FISICA' }, status: { code: 1, description: 'ACTIVO' }, primaryAddress: null, primaryPhone: null, roles: [] }], page: 1, pageSize: 20, total: 1 })
    fireEvent.change(screen.getByLabelText('Nombre o razón social'), { target: { value: 'Cliente' } })
    fireEvent.click(screen.getByRole('button', { name: 'Buscar clientes' }))
    expect(await screen.findByText('ABC0******B1')).toBeTruthy()
    expect(screen.getByText('Editar cliente · Próximamente')).toHaveProperty('disabled', true)
  })

  it('renders the authorized detail with active phone and emails', async () => {
    searchMock.mockResolvedValue({ items: [{ personId: 42, rfcMasked: 'ABC0******B1', name: 'Cliente', legalPersonality: { code: 1, description: 'FISICA' }, status: { code: 1, description: 'ACTIVO' }, primaryAddress: null, primaryPhone: null, roles: [] }], page: 1, pageSize: 20, total: 1 })
    detailMock.mockResolvedValue({ personId: 42, rfc: 'ABC010203AB1', name: 'Cliente', legalPersonality: { code: 1, description: 'FISICA' }, status: { code: 1, description: 'ACTIVO' }, primaryAddress: null, primaryPhone: { phoneId: 2, areaCode: '55', phoneNumber: '5555555555', extension: null, isDefault: true }, activePhones: [{ phoneId: 2, areaCode: '55', phoneNumber: '5555555555', extension: null, isDefault: true }], activeEmails: [{ emailId: 1, contact: 'Contacto', email: 'cliente@example.test' }], roles: [] })
    emailsMock.mockResolvedValue([{ emailId: 1, personId: 42, contact: 'Contacto', email: 'cliente@example.test', status: 'Active', usageCodes: [1], modifiedAt: '2025-01-01T00:00:00Z' }])
    readinessMock.mockResolvedValue({ personId: 42, hasGeneralData: true, hasAddress: true, hasPhone: true, hasAccount: true, canCreateContract: true, missingRequirements: [] })
    render(<CustomersView productName="uCredit-auto" customerName="Toyota Financial Services" onUnauthorized={unauthorized} />)
    fireEvent.change(screen.getByLabelText('Identificador'), { target: { value: '42' } })
    fireEvent.click(screen.getByRole('button', { name: 'Buscar clientes' }))
    fireEvent.click(await screen.findByRole('button', { name: 'Ver detalle' }))
    expect((await screen.findAllByText('55 5555555555')).length).toBeGreaterThanOrEqual(1)
    expect(screen.getAllByText('cliente@example.test').length).toBeGreaterThanOrEqual(2)
    expect(screen.getByText('Cliente habilitado para contratos.')).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Capturar contrato' })).toHaveProperty('disabled', false)
  })

  it('shows incomplete readiness and controlled missing requirements', async () => {
    searchMock.mockResolvedValue({ items: [{ personId: 42, rfcMasked: 'ABC0******B1', name: 'Cliente', legalPersonality: { code: 1, description: 'FISICA' }, status: { code: 1, description: 'ACTIVO' }, primaryAddress: null, primaryPhone: null, roles: [] }], page: 1, pageSize: 20, total: 1 })
    detailMock.mockResolvedValue({ personId: 42, rfc: null, name: 'Cliente', legalPersonality: { code: 1, description: 'FISICA' }, status: { code: 1, description: 'ACTIVO' }, primaryAddress: null, primaryPhone: null, activePhones: [], activeEmails: [], roles: [] })
    readinessMock.mockResolvedValue({ personId: 42, hasGeneralData: true, hasAddress: true, hasPhone: true, hasAccount: false, canCreateContract: false, missingRequirements: ['account'] })
    render(<CustomersView productName="uCredit-auto" customerName="Toyota Financial Services" onUnauthorized={unauthorized} />)
    fireEvent.change(screen.getByLabelText('Identificador'), { target: { value: '42' } })
    fireEvent.click(screen.getByRole('button', { name: 'Buscar clientes' }))
    fireEvent.click(await screen.findByRole('button', { name: 'Ver detalle' }))
    expect(await screen.findByText('Este cliente todavía no puede tener contratos.')).toBeTruthy()
    expect(screen.getByText('Cuenta')).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Capturar contrato · Próximamente' })).toHaveProperty('disabled', true)
  })

  it('does not expose or submit a domicile when managing a phone', async () => {
    configureAccountDetail([])
    phonesMock.mockResolvedValue([{ phoneId: 7, personId: 42, phoneTypeCode: 3, longDistanceCode: null, areaCode: '55', phoneNumber: '5555555555', extension: null, status: 'Active', inactiveReason: null, isDefault: true, modifiedAt: '2025-01-01T00:00:00Z', contactName: null }])
    await openAccountDetail()
    const phones = screen.getByRole('heading', { name: 'Teléfonos' }).closest('section') as HTMLElement
    fireEvent.click(phones.querySelector('button') as HTMLButtonElement)
    expect(screen.queryByLabelText('Domicilio asociado')).toBeNull()
    fireEvent.change(screen.getByLabelText('Número'), { target: { value: '5555555555' } })
    fireEvent.click(screen.getByRole('button', { name: 'Guardar teléfono' }))
    await vi.waitFor(() => expect(createPhoneMock).toHaveBeenCalledOnce())
    expect(createPhoneMock.mock.calls[0][1]).not.toHaveProperty('addressId')
  })

  it('ends the session on unauthorized response', async () => {
    searchMock.mockRejectedValue(new ApiError(401))
    render(<CustomersView productName="uCredit-auto" customerName="Toyota Financial Services" onUnauthorized={unauthorized} />)
    fireEvent.change(screen.getByLabelText('RFC'), { target: { value: 'ABC010203AB1' } })
    fireEvent.click(screen.getByRole('button', { name: 'Buscar clientes' }))
    await vi.waitFor(() => expect(unauthorized).toHaveBeenCalledOnce())
  })

  it('renders masked accounts and never preloads sensitive values in the edit panel', async () => {
    searchMock.mockResolvedValue({ items: [{ personId: 42, rfcMasked: 'ABC0******B1', name: 'Cliente', legalPersonality: { code: 1, description: 'FISICA' }, status: { code: 1, description: 'ACTIVO' }, primaryAddress: null, primaryPhone: null, roles: [] }], page: 1, pageSize: 20, total: 1 })
    detailMock.mockResolvedValue({ personId: 42, rfc: null, name: 'Cliente', legalPersonality: { code: 1, description: 'FISICA' }, status: { code: 1, description: 'ACTIVO' }, primaryAddress: null, primaryPhone: null, activePhones: [], activeEmails: [], roles: [] })
    readinessMock.mockResolvedValue({ personId: 42, hasGeneralData: true, hasAddress: true, hasPhone: true, hasAccount: true, canCreateContract: true, missingRequirements: [] })
    accountsMock.mockResolvedValue([accountFixture])
    render(<CustomersView productName="uCredit-auto" customerName="Toyota Financial Services" onUnauthorized={unauthorized} canCreate />)
    fireEvent.change(screen.getByLabelText('Identificador'), { target: { value: '42' } })
    fireEvent.click(screen.getByRole('button', { name: 'Buscar clientes' }))
    fireEvent.click(await screen.findByRole('button', { name: 'Ver detalle' }))
    expect(await screen.findByText(/0001/)).toBeTruthy()
    fireEvent.click(accountSection().querySelectorAll('button')[1] as HTMLButtonElement)
    await screen.findByRole('option', { name: 'Banco Alfa' })
    expect(screen.getAllByPlaceholderText('Dejar vacío para conservar')).toHaveLength(2)
    expect(screen.getByLabelText('Número de cuenta')).toHaveProperty('value', '')
    expect(screen.getByLabelText('CLABE')).toHaveProperty('value', '')
  })

  it('shows an empty account list and opens and closes the create panel', async () => {
    configureAccountDetail([])
    await openAccountDetail()
    expect(screen.getByText('No hay cuentas registradas.')).toBeTruthy()
    fireEvent.click(accountSection().querySelector('button') as HTMLButtonElement)
    expect(screen.getByRole('dialog', { name: 'Agregar cuenta' })).toBeTruthy()
    fireEvent.click(screen.getByRole('button', { name: 'Cancelar' }))
    expect(screen.queryByRole('dialog', { name: 'Agregar cuenta' })).toBeNull()
  })

  it('creates once, refreshes accounts and readiness, and never leaves sensitive values in the DOM', async () => {
    configureAccountDetail([])
    accountsMock.mockReset().mockResolvedValueOnce([]).mockResolvedValueOnce([accountFixture])
    let completeCreate!: (value: typeof accountFixture) => void
    createAccountMock.mockReturnValue(new Promise(resolve => { completeCreate = resolve }) as never)
    await openAccountDetail()
    fireEvent.click(accountSection().querySelector('button') as HTMLButtonElement)
    await screen.findByRole('option', { name: 'Banco Alfa' })
    fireEvent.change(screen.getByLabelText('Banco'), { target: { value: '2' } })
    fireEvent.change(screen.getByLabelText('Sucursal'), { target: { value: '12' } })
    fireEvent.change(screen.getByLabelText('Número de cuenta'), { target: { value: 'SYNTHETIC-ACCOUNT' } })
    fireEvent.change(screen.getByLabelText('CLABE'), { target: { value: 'SYNTHETIC-CLABE-18' } })
    const saveButton = screen.getByRole('button', { name: 'Guardar cuenta' })
    fireEvent.click(saveButton)
    await vi.waitFor(() => expect(createAccountMock).toHaveBeenCalledOnce())
    expect(createAccountMock.mock.calls[0][0]).toBe(42)
    expect(createAccountMock.mock.calls[0][1].bankId).toBe(2)
    fireEvent.click(saveButton)
    expect(createAccountMock).toHaveBeenCalledOnce()
    completeCreate(accountFixture)
    await vi.waitFor(() => expect(accountsMock).toHaveBeenCalledTimes(2))
    expect(readinessMock).toHaveBeenCalledTimes(2)
    expect(document.body.textContent).not.toContain('SYNTHETIC-ACCOUNT')
    expect(document.body.textContent).not.toContain('SYNTHETIC-CLABE-18')
  })

  it('edits while preserving omitted sensitive values and can replace only one value', async () => {
    configureAccountDetail()
    await openAccountDetail()
    fireEvent.click(accountSection().querySelectorAll('button')[1] as HTMLButtonElement)
    await screen.findByRole('option', { name: 'Banco Alfa' })
    expect(screen.getAllByPlaceholderText('Dejar vacío para conservar')).toHaveLength(2)
    fireEvent.change(screen.getByLabelText('CLABE'), { target: { value: 'SYNTHETIC-NEW-CLABE' } })
    fireEvent.click(screen.getByRole('button', { name: 'Guardar cuenta' }))
    await vi.waitFor(() => expect(updateAccountMock).toHaveBeenCalledOnce())
    const payload = updateAccountMock.mock.calls[0][2]
    expect(payload.accountNumber).toBeNull()
    expect(payload.clabe).toBe('SYNTHETIC-NEW-CLABE')
  })

  it('renders active real banks in a required selector without a manual bank id input', async () => {
    configureAccountDetail([])
    await openAccountDetail()
    fireEvent.click(accountSection().querySelector('button') as HTMLButtonElement)
    await screen.findByRole('option', { name: 'Banco Alfa' })
    expect(screen.getByLabelText('Banco').tagName).toBe('SELECT')
    expect(screen.getByRole('option', { name: 'Selecciona un banco' })).toBeTruthy()
    expect(screen.getByRole('option', { name: 'Banco Alfa' })).toBeTruthy()
    expect(screen.getByRole('option', { name: 'Banco Beta' })).toBeTruthy()
    expect(screen.queryByRole('spinbutton', { name: 'Banco' })).toBeNull()
  })

  it('disables account saving and announces a catalog error or empty catalog', async () => {
    configureAccountDetail([])
    banksMock.mockRejectedValueOnce(new ApiError(503))
    await openAccountDetail()
    fireEvent.click(accountSection().querySelector('button') as HTMLButtonElement)
    const catalogAlert = await screen.findByRole('alert')
    expect(catalogAlert.textContent).toContain('No fue posible cargar el catálogo de bancos.')
    expect(screen.getByRole('button', { name: 'Guardar cuenta' })).toHaveProperty('disabled', true)

    cleanup()
    configureAccountDetail([])
    banksMock.mockResolvedValueOnce([])
    await openAccountDetail()
    fireEvent.click(accountSection().querySelector('button') as HTMLButtonElement)
    expect(await screen.findByText('No existen bancos disponibles para registrar la cuenta.')).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Guardar cuenta' })).toHaveProperty('disabled', true)
  })

  it('preserves an historical unavailable bank only while editing the existing account', async () => {
    configureAccountDetail([{ ...accountFixture, bankId: 9, bankName: 'Banco histórico' }])
    await openAccountDetail()
    fireEvent.click(accountSection().querySelectorAll('button')[1] as HTMLButtonElement)
    await screen.findByRole('option', { name: 'Banco Alfa' })
    const historical = await screen.findByRole('option', { name: 'Banco histórico (no disponible)' })
    expect(historical).toHaveProperty('disabled', true)
    expect(screen.getByLabelText('Banco')).toHaveProperty('value', '9')
  })

  it('activates and deactivates accounts while refreshing the account list and readiness', async () => {
    configureAccountDetail([{ ...accountFixture, status: 1 }])
    accountsMock.mockReset().mockResolvedValueOnce([{ ...accountFixture, status: 1 }]).mockResolvedValueOnce([{ ...accountFixture, status: 2 }]).mockResolvedValueOnce([{ ...accountFixture, status: 1 }])
    await openAccountDetail()
    vi.spyOn(window, 'confirm').mockReturnValue(true)
    fireEvent.click(screen.getByRole('button', { name: 'Desactivar' }))
    await vi.waitFor(() => expect(deactivateAccountMock).toHaveBeenCalledOnce())
    await vi.waitFor(() => expect(accountsMock).toHaveBeenCalledTimes(2))
    await vi.waitFor(() => expect(screen.getByText(/Inactivo/)).toBeTruthy())
    fireEvent.click(screen.getByRole('button', { name: 'Activar' }))
    await vi.waitFor(() => expect(activateAccountMock).toHaveBeenCalledOnce())
    await vi.waitFor(() => expect(accountsMock).toHaveBeenCalledTimes(3))
    expect(readinessMock).toHaveBeenCalledTimes(3)
    vi.restoreAllMocks()
  })

  it.each([
    [400, undefined], [403, undefined], [404, undefined], [503, undefined],
  ])('shows controlled account errors for HTTP %s', async (status, code) => {
    configureAccountDetail([])
    createAccountMock.mockRejectedValue(new ApiError(status, code))
    await openAccountDetail()
    fireEvent.click(accountSection().querySelector('button') as HTMLButtonElement)
    await screen.findByRole('option', { name: 'Banco Alfa' })
    fireEvent.change(screen.getByLabelText('Banco'), { target: { value: '2' } })
    fireEvent.change(screen.getByLabelText('Sucursal'), { target: { value: '12' } })
    fireEvent.change(screen.getByLabelText('Número de cuenta'), { target: { value: 'SYNTHETIC-ACCOUNT' } })
    fireEvent.change(screen.getByLabelText('CLABE'), { target: { value: 'SYNTHETIC-CLABE-18' } })
    fireEvent.click(screen.getByRole('button', { name: 'Guardar cuenta' }))
    expect(await screen.findByRole('status')).toBeTruthy()
  })

  it('shows account duplicate and modified conflicts without retrying', async () => {
    configureAccountDetail()
    createAccountMock.mockRejectedValue(new ApiError(409, 'account_duplicate'))
    updateAccountMock.mockRejectedValue(new ApiError(409, 'account_modified'))
    await openAccountDetail()
    fireEvent.click(accountSection().querySelector('button') as HTMLButtonElement)
    await screen.findByRole('option', { name: 'Banco Alfa' })
    fireEvent.change(screen.getByLabelText('Banco'), { target: { value: '2' } })
    fireEvent.change(screen.getByLabelText('Sucursal'), { target: { value: '12' } })
    fireEvent.change(screen.getByLabelText('Número de cuenta'), { target: { value: 'SYNTHETIC-ACCOUNT' } })
    fireEvent.change(screen.getByLabelText('CLABE'), { target: { value: 'SYNTHETIC-CLABE-18' } })
    fireEvent.click(screen.getByRole('button', { name: 'Guardar cuenta' }))
    await vi.waitFor(() => expect(screen.getAllByRole('status').some(item => item.textContent?.includes('Ya existe una cuenta'))).toBe(true))
    expect(createAccountMock).toHaveBeenCalledOnce()
    fireEvent.click(screen.getByRole('button', { name: 'Cancelar' }))
    fireEvent.click(accountSection().querySelectorAll('button')[1] as HTMLButtonElement)
    await screen.findByRole('option', { name: 'Banco Alfa' })
    fireEvent.click(screen.getByRole('button', { name: 'Guardar cuenta' }))
    await vi.waitFor(() => expect(screen.getAllByRole('status').some(item => item.textContent?.includes('La cuenta cambió'))).toBe(true))
    expect(updateAccountMock).toHaveBeenCalledOnce()
  })

  it('returns to login on HTTP 401 from account creation', async () => {
    configureAccountDetail([])
    createAccountMock.mockRejectedValue(new ApiError(401))
    await openAccountDetail()
    fireEvent.click(accountSection().querySelector('button') as HTMLButtonElement)
    await screen.findByRole('option', { name: 'Banco Alfa' })
    fireEvent.change(screen.getByLabelText('Banco'), { target: { value: '2' } })
    fireEvent.change(screen.getByLabelText('Sucursal'), { target: { value: '12' } })
    fireEvent.change(screen.getByLabelText('Número de cuenta'), { target: { value: 'SYNTHETIC-ACCOUNT' } })
    fireEvent.change(screen.getByLabelText('CLABE'), { target: { value: 'SYNTHETIC-CLABE-18' } })
    fireEvent.click(screen.getByRole('button', { name: 'Guardar cuenta' }))
    await vi.waitFor(() => expect(unauthorized).toHaveBeenCalledOnce())
  })

  it('creates an email with the dynamic invoice usage selected by default', async () => {
    configureAccountDetail([])
    let resolveCreate!: (value: unknown) => void
    createEmailMock.mockReturnValue(new Promise(resolve => { resolveCreate = resolve }) as never)
    await openAccountDetail()
    const section = screen.getByRole('heading', { name: 'Correos' }).closest('section') as HTMLElement
    fireEvent.click(section.querySelector('button') as HTMLButtonElement)
    const dialog = await screen.findByRole('dialog', { name: 'Agregar correo' })
    fireEvent.change(screen.getByLabelText('Correo electrónico'), { target: { value: 'nuevo@example.invalid' } })
    fireEvent.click(screen.getByRole('button', { name: 'Guardar correo' }))
    await vi.waitFor(() => expect(screen.getByRole('button', { name: 'Guardando…' })).toBeTruthy())
    fireEvent.click(screen.getByRole('button', { name: 'Guardando…' }))
    expect(createEmailMock).toHaveBeenCalledOnce()
    resolveCreate({})
    await vi.waitFor(() => expect(createEmailMock).toHaveBeenCalledOnce())
    expect(createEmailMock.mock.calls[0][1]).toMatchObject({ email: 'nuevo@example.invalid', usageCodes: [1] })
    await vi.waitFor(() => expect(readinessMock).toHaveBeenCalledTimes(2))
    expect(dialog).toBeTruthy()
  })

  it('renders empty and multiple email collections without changing readiness', async () => {
    configureAccountDetail([])
    await openAccountDetail()
    expect(screen.getByText('No hay correos registrados.')).toBeTruthy()
    expect(readinessMock).toHaveBeenCalledOnce()

    cleanup()
    configureAccountDetail([])
    readinessMock.mockClear()
    emailsMock.mockResolvedValue([
      { emailId: 8, personId: 42, contact: null, email: 'uno@example.invalid', status: 'Active', usageCodes: [1], modifiedAt: '2025-01-01T00:00:00Z' },
      { emailId: 9, personId: 42, contact: null, email: 'dos@example.invalid', status: 'Inactive', usageCodes: [2], modifiedAt: '2025-01-02T00:00:00Z' },
    ])
    await openAccountDetail()
    expect(screen.getByText('uno@example.invalid')).toBeTruthy()
    expect(screen.getByText('Correo inactivo')).toBeTruthy()
    expect(readinessMock).toHaveBeenCalledOnce()
  })

  it('requires at least one usage and does not submit when the last usage is removed', async () => {
    configureAccountDetail([])
    await openAccountDetail()
    const section = screen.getByRole('heading', { name: 'Correos' }).closest('section') as HTMLElement
    fireEvent.click(section.querySelector('button') as HTMLButtonElement)
    await screen.findByRole('dialog', { name: 'Agregar correo' })
    fireEvent.change(screen.getByLabelText('Correo electrónico'), { target: { value: 'nuevo@example.invalid' } })
    fireEvent.click(screen.getByRole('checkbox', { name: 'Envío de facturas' }))
    fireEvent.submit(screen.getByRole('dialog').querySelector('form') as HTMLFormElement)
    expect((await screen.findByRole('alert')).textContent).toContain('Selecciona al menos un uso de correo.')
    expect(createEmailMock).not.toHaveBeenCalled()
  })

  it('shows duplicate and modified email conflicts without retrying', async () => {
    configureAccountDetail([])
    createEmailMock.mockRejectedValue(new ApiError(409, 'email_duplicate'))
    await openAccountDetail()
    const section = screen.getByRole('heading', { name: 'Correos' }).closest('section') as HTMLElement
    fireEvent.click(section.querySelector('button') as HTMLButtonElement)
    await screen.findByRole('dialog', { name: 'Agregar correo' })
    fireEvent.change(screen.getByLabelText('Correo electrónico'), { target: { value: 'duplicado@example.invalid' } })
    fireEvent.click(screen.getByRole('button', { name: 'Guardar correo' }))
    await vi.waitFor(() => expect(screen.getAllByRole('status').some(item => item.textContent?.includes('Ya existe ese correo activo'))).toBe(true))
    expect(createEmailMock).toHaveBeenCalledOnce()

    fireEvent.click(screen.getByRole('button', { name: 'Cancelar' }))
    cleanup()
    emailsMock.mockResolvedValue([{ emailId: 8, personId: 42, contact: null, email: 'existente@example.invalid', status: 'Active', usageCodes: [1], modifiedAt: '2025-01-01T00:00:00Z' }])
    updateEmailMock.mockRejectedValue(new ApiError(409, 'email_modified'))
    await openAccountDetail()
    fireEvent.click((screen.getByRole('heading', { name: 'Correos' }).closest('section') as HTMLElement).querySelectorAll('button')[1] as HTMLButtonElement)
    await screen.findByPlaceholderText('Dejar vacío para conservar')
    fireEvent.click(screen.getByRole('button', { name: 'Guardar correo' }))
    await vi.waitFor(() => expect(screen.getAllByRole('status').some(item => item.textContent?.includes('El correo cambió'))).toBe(true))
    expect(updateEmailMock).toHaveBeenCalledOnce()
  })

  it.each([400, 403, 404, 503])('shows controlled email error for HTTP %s', async status => {
    configureAccountDetail([])
    createEmailMock.mockRejectedValue(new ApiError(status))
    await openAccountDetail()
    const section = screen.getByRole('heading', { name: 'Correos' }).closest('section') as HTMLElement
    fireEvent.click(section.querySelector('button') as HTMLButtonElement)
    await screen.findByRole('dialog', { name: 'Agregar correo' })
    fireEvent.change(screen.getByLabelText('Correo electrónico'), { target: { value: 'error@example.invalid' } })
    fireEvent.click(screen.getByRole('button', { name: 'Guardar correo' }))
    expect(await screen.findByRole('status')).toBeTruthy()
    await vi.waitFor(() => expect(createEmailMock).toHaveBeenCalledOnce())
  })

  it('returns to login on email 401 and reports catalog failure or emptiness', async () => {
    configureAccountDetail([])
    createEmailMock.mockRejectedValue(new ApiError(401))
    await openAccountDetail()
    const section = screen.getByRole('heading', { name: 'Correos' }).closest('section') as HTMLElement
    fireEvent.click(section.querySelector('button') as HTMLButtonElement)
    await screen.findByRole('dialog', { name: 'Agregar correo' })
    await screen.findByRole('checkbox', { name: 'Envío de facturas' })
    fireEvent.change(screen.getByLabelText('Correo electrónico'), { target: { value: 'unauthorized@example.invalid' } })
    fireEvent.click(screen.getByRole('button', { name: 'Guardar correo' }))
    await vi.waitFor(() => expect(unauthorized).toHaveBeenCalledOnce())

    cleanup()
    configureAccountDetail([])
    emailUsagesMock.mockResolvedValue([])
    await openAccountDetail()
    fireEvent.click((screen.getByRole('heading', { name: 'Correos' }).closest('section') as HTMLElement).querySelector('button') as HTMLButtonElement)
    expect(await screen.findByText('No hay usos de correo disponibles.')).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Guardar correo' })).toHaveProperty('disabled', true)
  })

  it('does not preload an existing email in the edit panel', async () => {
    configureAccountDetail([])
    emailsMock.mockResolvedValue([{ emailId: 8, personId: 42, contact: 'Contacto', email: 'existente@example.invalid', status: 'Active', usageCodes: [1], modifiedAt: '2025-01-01T00:00:00Z' }])
    await openAccountDetail()
    const section = screen.getByRole('heading', { name: 'Correos' }).closest('section') as HTMLElement
    fireEvent.click((screen.getByRole('heading', { name: 'Correos' }).closest('section') as HTMLElement).querySelectorAll('button')[1] as HTMLButtonElement)
    expect(await screen.findByPlaceholderText('Dejar vacío para conservar')).toHaveProperty('value', '')
    expect(section.textContent).toContain('existente@example.invalid')
  })

  it('refreshes email state and readiness after deactivation', async () => {
    configureAccountDetail([])
    const email = { emailId: 8, personId: 42, contact: null, email: 'activo@example.invalid', status: 'Active' as const, usageCodes: [1], modifiedAt: '2025-01-01T00:00:00Z' }
    emailsMock.mockResolvedValue([email])
    deactivateEmailMock.mockResolvedValue({ ...email, status: 'Inactive' } as never)
    await openAccountDetail()
    vi.spyOn(window, 'confirm').mockReturnValue(true)
    fireEvent.click(screen.getByRole('button', { name: 'Desactivar' }))
    await vi.waitFor(() => expect(deactivateEmailMock).toHaveBeenCalledOnce())
    await vi.waitFor(() => expect(readinessMock).toHaveBeenCalledTimes(2))
    vi.restoreAllMocks()
  })

  it('opens and cancels general editing without changing other expediente sections', async () => {
    configureAccountDetail([])
    await openAccountDetail()
    const section = screen.getByRole('heading', { name: 'Datos generales' }).closest('section') as HTMLElement
    fireEvent.click(section.querySelector('button') as HTMLButtonElement)
    expect(await screen.findByLabelText('Nombre')).toHaveProperty('value', 'Nombre')
    expect(section.textContent).toContain('ABC010203AB1')
    fireEvent.click(screen.getByRole('button', { name: 'Cancelar' }))
    expect(screen.queryByLabelText('Nombre')).toBeNull()
    expect(screen.getByRole('heading', { name: 'Cuentas' })).toBeTruthy()
    expect(screen.getByRole('heading', { name: 'Correos' })).toBeTruthy()
  })

  it('edits names and roles once, confirms removal, and keeps inactive roles unavailable', async () => {
    configureAccountDetail([])
    await openAccountDetail()
    const section = screen.getByRole('heading', { name: 'Datos generales' }).closest('section') as HTMLElement
    fireEvent.click(section.querySelector('button') as HTMLButtonElement)
    expect(await screen.findByLabelText('Nombre')).toBeTruthy()
    expect(section.querySelectorAll('input[type="checkbox"]')[1]).toHaveProperty('disabled', true)
    fireEvent.change(screen.getByLabelText('Nombre'), { target: { value: 'Nombre nuevo' } })
    fireEvent.click(screen.getByRole('button', { name: 'Guardar datos generales' }))
    await vi.waitFor(() => expect(updateGeneralMock).toHaveBeenCalledOnce())
    expect(updateGeneralMock.mock.calls[0][1]).not.toHaveProperty('rfc')
    expect(updateGeneralMock.mock.calls[0][1]).not.toHaveProperty('statusCode')
  })

  it('shows controlled customer_modified and prevents a second general update', async () => {
    configureAccountDetail([])
    updateGeneralMock.mockRejectedValue(new ApiError(409, 'customer_modified'))
    await openAccountDetail()
    const section = screen.getByRole('heading', { name: 'Datos generales' }).closest('section') as HTMLElement
    fireEvent.click(section.querySelector('button') as HTMLButtonElement)
    await screen.findByLabelText('Nombre')
    const save = screen.getByRole('button', { name: 'Guardar datos generales' })
    fireEvent.click(save)
    fireEvent.click(save)
    await vi.waitFor(() => expect(screen.getByRole('alert').textContent).toContain('Los datos cambiaron'))
    expect(updateGeneralMock).toHaveBeenCalledOnce()
  })
})
