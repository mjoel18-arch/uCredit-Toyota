import React from 'react'
import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { ApiError } from '../../src/shared/api/apiClient'
import { getCustomerAddresses, getCustomerByPersonId, getCustomerProfileReadiness, searchCustomers } from '../../src/features/auth/authApi'
import { CustomersView } from '../../src/features/customers/CustomersView'

vi.mock('../../src/features/auth/authApi', async () => {
  const actual = await vi.importActual<typeof import('../../src/features/auth/authApi')>('../../src/features/auth/authApi')
  return { ...actual, getCustomerAddresses: vi.fn(), getCustomerByPersonId: vi.fn(), getCustomerProfileReadiness: vi.fn(), searchCustomers: vi.fn() }
})

const searchMock = vi.mocked(searchCustomers)
const detailMock = vi.mocked(getCustomerByPersonId)
const readinessMock = vi.mocked(getCustomerProfileReadiness)
const addressesMock = vi.mocked(getCustomerAddresses)
const unauthorized = vi.fn()

beforeEach(() => { searchMock.mockReset(); detailMock.mockReset(); readinessMock.mockReset(); addressesMock.mockReset(); addressesMock.mockResolvedValue([]); unauthorized.mockReset() })
afterEach(() => cleanup())

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
    expect((await screen.findAllByText('55 5555555555')).length).toBeGreaterThanOrEqual(2)
    expect(screen.getAllByText('cliente@example.test').length).toBeGreaterThanOrEqual(2)
    expect(screen.getByText('Cliente habilitado para contratos.')).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Capturar contrato · Próximamente' })).toHaveProperty('disabled', true)
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

  it('ends the session on unauthorized response', async () => {
    searchMock.mockRejectedValue(new ApiError(401))
    render(<CustomersView productName="uCredit-auto" customerName="Toyota Financial Services" onUnauthorized={unauthorized} />)
    fireEvent.change(screen.getByLabelText('RFC'), { target: { value: 'ABC010203AB1' } })
    fireEvent.click(screen.getByRole('button', { name: 'Buscar clientes' }))
    await vi.waitFor(() => expect(unauthorized).toHaveBeenCalledOnce())
  })
})
