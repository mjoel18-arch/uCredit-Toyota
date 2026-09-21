import React from 'react'
import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { ApiError } from '../../src/shared/api/apiClient'
import { activateCustomerAccount, activateCustomerPhone, createCustomerAccount, createCustomerPhone, deactivateCustomerAccount, deactivateCustomerPhone, getCustomerAccounts, getCustomerAddresses, getCustomerBanks, getCustomerByPersonId, getCustomerPhones, getCustomerProfileReadiness, searchCustomers, updateCustomerAccount, updateCustomerPhone } from '../../src/features/auth/authApi'
import { CustomersView } from '../../src/features/customers/CustomersView'

vi.mock('../../src/features/auth/authApi', async () => {
  const actual = await vi.importActual<typeof import('../../src/features/auth/authApi')>('../../src/features/auth/authApi')
  return { ...actual, activateCustomerAccount: vi.fn(), activateCustomerPhone: vi.fn(), createCustomerAccount: vi.fn(), createCustomerPhone: vi.fn(), deactivateCustomerAccount: vi.fn(), deactivateCustomerPhone: vi.fn(), getCustomerAccounts: vi.fn(), getCustomerAddresses: vi.fn(), getCustomerBanks: vi.fn(), getCustomerByPersonId: vi.fn(), getCustomerPhones: vi.fn(), getCustomerProfileReadiness: vi.fn(), searchCustomers: vi.fn(), updateCustomerAccount: vi.fn(), updateCustomerPhone: vi.fn() }
})

const searchMock = vi.mocked(searchCustomers)
const detailMock = vi.mocked(getCustomerByPersonId)
const readinessMock = vi.mocked(getCustomerProfileReadiness)
const addressesMock = vi.mocked(getCustomerAddresses)
const phonesMock = vi.mocked(getCustomerPhones)
const accountsMock = vi.mocked(getCustomerAccounts)
const banksMock = vi.mocked(getCustomerBanks)
const createAccountMock = vi.mocked(createCustomerAccount)
const updateAccountMock = vi.mocked(updateCustomerAccount)
const activateAccountMock = vi.mocked(activateCustomerAccount)
const deactivateAccountMock = vi.mocked(deactivateCustomerAccount)
const createPhoneMock = vi.mocked(createCustomerPhone)
const updatePhoneMock = vi.mocked(updateCustomerPhone)
const activatePhoneMock = vi.mocked(activateCustomerPhone)
const deactivatePhoneMock = vi.mocked(deactivateCustomerPhone)
const unauthorized = vi.fn()

beforeEach(() => { searchMock.mockReset(); detailMock.mockReset(); readinessMock.mockReset(); addressesMock.mockReset(); phonesMock.mockReset(); accountsMock.mockReset(); banksMock.mockReset(); createAccountMock.mockReset(); updateAccountMock.mockReset(); activateAccountMock.mockReset(); deactivateAccountMock.mockReset(); createPhoneMock.mockReset(); updatePhoneMock.mockReset(); activatePhoneMock.mockReset(); deactivatePhoneMock.mockReset(); addressesMock.mockResolvedValue([]); phonesMock.mockResolvedValue([]); accountsMock.mockResolvedValue([]); banksMock.mockResolvedValue([{ bankId: 2, bankName: 'Banco Alfa' }, { bankId: 4, bankName: 'Banco Beta' }]); createAccountMock.mockResolvedValue({} as never); updateAccountMock.mockResolvedValue({} as never); activateAccountMock.mockResolvedValue({} as never); deactivateAccountMock.mockResolvedValue({} as never); createPhoneMock.mockResolvedValue({} as never); updatePhoneMock.mockResolvedValue({} as never); activatePhoneMock.mockResolvedValue({} as never); deactivatePhoneMock.mockResolvedValue({} as never); unauthorized.mockReset() })
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
    fireEvent.click(screen.getByRole('button', { name: 'Editar' }))
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
    fireEvent.click(screen.getByRole('button', { name: 'Editar' }))
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
    fireEvent.click(screen.getByRole('button', { name: 'Editar' }))
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
    fireEvent.click(screen.getByRole('button', { name: 'Editar' }))
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
})
