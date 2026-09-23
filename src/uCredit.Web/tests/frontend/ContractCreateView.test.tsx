import React from 'react'
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { ContractCreateView } from '../../src/features/contracts/ContractCreateView'
import { ApiError } from '../../src/shared/api/apiClient'
import { getContractAddresses, getContractCfdiUses, getContractCnbv, getContractLateRate, getContractOperations, getContractOrdinaryRate, getCustomerByPersonId, getCustomerProfileReadiness, previewContract } from '../../src/features/auth/authApi'

vi.mock('../../src/features/auth/authApi', async () => {
  const actual = await vi.importActual<typeof import('../../src/features/auth/authApi')>('../../src/features/auth/authApi')
  return { ...actual, getContractAddresses: vi.fn(), getContractCfdiUses: vi.fn(), getContractCnbv: vi.fn(), getContractLateRate: vi.fn(), getContractOperations: vi.fn(), getContractOrdinaryRate: vi.fn(), getCustomerByPersonId: vi.fn(), getCustomerProfileReadiness: vi.fn(), previewContract: vi.fn() }
})

const mocks = {
  addresses: vi.mocked(getContractAddresses),
  cfdi: vi.mocked(getContractCfdiUses),
  cnbv: vi.mocked(getContractCnbv),
  lateRate: vi.mocked(getContractLateRate),
  operations: vi.mocked(getContractOperations),
  rate: vi.mocked(getContractOrdinaryRate),
  customer: vi.mocked(getCustomerByPersonId),
  readiness: vi.mocked(getCustomerProfileReadiness),
  preview: vi.mocked(previewContract),
}

beforeEach(() => {
  Object.values(mocks).forEach(mock => mock.mockReset())
  mocks.operations.mockResolvedValue([{ code: 'CD', description: 'Crédito directo', companyId: 1 }, { code: 'OT', description: 'Otra operación', companyId: 1 }])
  mocks.customer.mockResolvedValue({ personId: 42, name: 'Cliente sintético', rfc: null, legalPersonality: { code: 1, description: 'Física' }, status: { code: 1, description: 'Activo' }, primaryAddress: null, primaryPhone: null, roles: [], activePhones: [], activeEmails: [] })
  mocks.readiness.mockResolvedValue({ personId: 42, hasGeneralData: true, hasAddress: true, hasPhone: true, hasAccount: true, canCreateContract: true, missingRequirements: [] })
  mocks.cfdi.mockResolvedValue([{ code: 'G03', description: 'Gastos en general' }])
  mocks.addresses.mockResolvedValue([{ id: 7, typeCode: 1, typeDescription: 'Dirección única' }])
  mocks.cnbv.mockResolvedValue([{ id: 5, description: 'CNBV sintético', isDefault: true }])
  mocks.rate.mockResolvedValue({ rateId: 1, description: 'FIJA PESOS', isFixed: true, currencyCode: 1, isActive: true, hasRevision: false })
  mocks.lateRate.mockResolvedValue({ rateId: 1, calculationTypeId: 1, points: 0, factor: 0, currencyCode: 1, isConfigured: true })
  mocks.preview.mockResolvedValue({ canCreate: false, operationCode: 'CD', businessDate: '2026-09-22', amounts: { capital: 100, downPayment: 0, amountToFinance: 100, initialBalance: 100 }, resolvedCatalogs: { rateId: 1, cnbvId: 5, cfdiUseCode: 'G03', addressId: 7 }, pendingRules: [{ code: 'contract_payment_configuration_required', message: 'Configuración pendiente.' }] })
})

afterEach(() => cleanup())

describe('ContractCreateView', () => {
  it('loads the customer-dependent catalogs and keeps creation disabled', async () => {
    render(<ContractCreateView onBack={vi.fn()} onUnauthorized={vi.fn()} />)
    fireEvent.change(screen.getByLabelText('Identificador del cliente'), { target: { value: '42' } })
    fireEvent.click(screen.getByRole('button', { name: 'Cargar cliente' }))
    await waitFor(() => expect(screen.getByText(/Cliente sintético/)).toBeTruthy())
    expect(screen.getByRole('button', { name: 'Creación pendiente de configuración' })).toHaveProperty('disabled', true)
    expect(screen.getByText(/Integración pendiente\. No se consulta ni se envía KPR_FL_CVE\./)).toBeTruthy()
  })

  it('loads CNBV and rates only after selecting an operation and previews without a create endpoint', async () => {
    render(<ContractCreateView onBack={vi.fn()} onUnauthorized={vi.fn()} />)
    fireEvent.change(screen.getByLabelText('Identificador del cliente'), { target: { value: '42' } })
    fireEvent.click(screen.getByRole('button', { name: 'Cargar cliente' }))
    await waitFor(() => expect(screen.getByText(/Cliente sintético/)).toBeTruthy())
    fireEvent.change(screen.getByLabelText('Operación'), { target: { value: 'CD' } })
    await waitFor(() => expect(screen.getByText('FIJA PESOS · moneda 1')).toBeTruthy())
    expect(mocks.cnbv).toHaveBeenCalledWith('CD')
    expect(mocks.rate).toHaveBeenCalledWith('CD')
    expect(mocks.lateRate).toHaveBeenCalledWith('CD')
    expect(screen.queryByRole('button', { name: /crear contrato/i })).toBeNull()
  })

  it('reports catalog errors without creating anything', async () => {
    mocks.operations.mockRejectedValue(new ApiError(503))
    render(<ContractCreateView onBack={vi.fn()} onUnauthorized={vi.fn()} />)
    await waitFor(() => expect(screen.getByRole('alert')).toBeTruthy())
    expect(mocks.preview).not.toHaveBeenCalled()
  })

  it('shows the internal-error message when a customer read returns 500', async () => {
    mocks.customer.mockRejectedValueOnce(new ApiError(500))
    render(<ContractCreateView onBack={vi.fn()} onUnauthorized={vi.fn()} />)
    fireEvent.change(screen.getByLabelText('Identificador del cliente'), { target: { value: '759161' } })
    fireEvent.click(screen.getByRole('button', { name: 'Cargar cliente' }))

    const alert = await screen.findByRole('alert')
    expect(alert.textContent).toBe('Ocurrió un error interno al consultar la información del contrato.')
    expect(screen.queryByText(/Cliente sintético/)).toBeNull()
  })

  it('keeps 503 distinct from an unexpected 500 during customer reads', async () => {
    mocks.customer.mockRejectedValueOnce(new ApiError(503))
    render(<ContractCreateView onBack={vi.fn()} onUnauthorized={vi.fn()} />)
    fireEvent.change(screen.getByLabelText('Identificador del cliente'), { target: { value: '759161' } })
    fireEvent.click(screen.getByRole('button', { name: 'Cargar cliente' }))

    const alert = await screen.findByRole('alert')
    expect(alert.textContent).toBe('El servicio no está disponible en este momento.')
  })

  it('reports an address catalog 500 without presenting it as unavailable', async () => {
    mocks.addresses.mockRejectedValueOnce(new ApiError(500))
    render(<ContractCreateView onBack={vi.fn()} onUnauthorized={vi.fn()} />)
    fireEvent.change(screen.getByLabelText('Identificador del cliente'), { target: { value: '759161' } })
    fireEvent.click(screen.getByRole('button', { name: 'Cargar cliente' }))

    const alert = await screen.findByRole('alert')
    expect(alert.textContent).toBe('Ocurrió un error interno al consultar domicilios.')
    expect(alert.textContent).not.toContain('no está disponible')
    expect(screen.queryByText(/Cliente sintético/)).toBeNull()
  })

  it('keeps an address catalog 503 distinct from its internal error', async () => {
    mocks.addresses.mockRejectedValueOnce(new ApiError(503))
    render(<ContractCreateView onBack={vi.fn()} onUnauthorized={vi.fn()} />)
    fireEvent.change(screen.getByLabelText('Identificador del cliente'), { target: { value: '759161' } })
    fireEvent.click(screen.getByRole('button', { name: 'Cargar cliente' }))

    const alert = await screen.findByRole('alert')
    expect(alert.textContent).toBe('El servicio no está disponible en este momento.')
  })
})
