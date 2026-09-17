import React from 'react'
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { ApiError } from '../../src/shared/api/apiClient'
import {
  getContractAmortization,
  getContractByNumber,
  searchContracts,
  type ContractAmortization,
  type ContractDetail,
} from '../../src/features/auth/authApi'
import { ContractsView } from '../../src/features/contracts/ContractsView'

vi.mock('../../src/features/auth/authApi', async () => {
  const actual = await vi.importActual<typeof import('../../src/features/auth/authApi')>('../../src/features/auth/authApi')
  return {
    ...actual,
    getContractAmortization: vi.fn(),
    getContractByNumber: vi.fn(),
    searchContracts: vi.fn(),
  }
})

const getDetailMock = vi.mocked(getContractByNumber)
const getAmortizationMock = vi.mocked(getContractAmortization)
const searchMock = vi.mocked(searchContracts)
const onUnauthorized = vi.fn()

const detail: ContractDetail = {
  contractNumber: '454890CD',
  status: { code: 1, description: 'ACTIVO' },
  operationType: { code: 'CD', description: 'CRÉDITO DIRECTO' },
  customerName: 'Cliente de prueba',
  currencyCode: 'MXN',
  currencyName: 'PESO MEXICANO',
  financedAmount: 125000.5,
  outstandingBalance: 98765.25,
  currentTerm: 48,
  originalTerm: 60,
  startDate: '2026-01-02',
  activationDate: '2026-01-03',
  disbursementDate: null,
  firstPaymentDate: '2026-02-02',
  lastPaymentDate: null,
}

beforeEach(() => {
  getDetailMock.mockReset()
  getAmortizationMock.mockReset()
  getAmortizationMock.mockRejectedValue(new ApiError(404))
  searchMock.mockReset()
  onUnauthorized.mockReset()
})

afterEach(() => {
  cleanup()
  vi.unstubAllGlobals()
})

function renderAndSubmit() {
  render(<ContractsView onUnauthorized={onUnauthorized} />)
  fireEvent.change(screen.getByLabelText('Número de contrato'), { target: { value: '454890CD' } })
  fireEvent.click(screen.getByRole('button', { name: 'Buscar contrato' }))
}

const scheduleWithDownPayment: ContractAmortization = {
  contractNumber: '454890CD',
  financingType: 1,
  version: 7,
  downPayment: {
    paymentNumber: 0,
    status: 'Generated',
    startDate: '2026-01-01',
    endDate: '2026-01-31',
    dueDate: '2026-01-01',
    calculationBase: 1000,
    outstandingBalance: 1000,
    amortization: 1000,
    interest: 0,
    iva: 160,
    payment: 1000,
    paymentWithIva: 1160,
    totalPayment: 1160,
  },
  payments: [
    {
      paymentNumber: 1,
      status: 'Generated',
      startDate: '2026-02-01',
      endDate: '2026-02-28',
      dueDate: '2026-02-15',
      calculationBase: 10000,
      outstandingBalance: 9000,
      amortization: 1000,
      interest: 500,
      iva: 80,
      payment: 1500,
      paymentWithIva: 1580,
      totalPayment: 1580,
    },
  ],
}

describe('ContractsView', () => {
  it('shows a loading state while detail and exact search are pending', () => {
    getDetailMock.mockReturnValue(new Promise<ContractDetail>(() => undefined))
    searchMock.mockReturnValue(new Promise(() => undefined))

    renderAndSubmit()

    const button = screen.getByRole('button', { name: 'Consultando…' })
    expect(button).toHaveProperty('disabled', true)
  })

  it('renders the accessible detail card with formatted values and null fallback', async () => {
    getDetailMock.mockResolvedValue(detail)
    searchMock.mockResolvedValue({ items: [{ contractNumber: '454890CD' }], page: 1, pageSize: 10, total: 1 })

    renderAndSubmit()

    expect(await screen.findByRole('heading', { name: 'Contrato 454890CD' })).toBeTruthy()
    expect(screen.getByText('ACTIVO')).toBeTruthy()
    expect(screen.getByText('MXN — PESO MEXICANO')).toBeTruthy()
    expect(screen.getByText('48')).toBeTruthy()
    expect(screen.getByText('60')).toBeTruthy()
    expect(screen.getByText('CRÉDITO DIRECTO')).toBeTruthy()
    expect(screen.getByText('$125,000.50')).toBeTruthy()
    expect(screen.getByText('$98,765.25')).toBeTruthy()
    expect(screen.getByText('2 ene 2026')).toBeTruthy()
    expect(screen.getByText('3 ene 2026')).toBeTruthy()
    expect(screen.getAllByText('No disponible').length).toBeGreaterThanOrEqual(2)
  })

  it('renders the amortization version, down payment, currency amounts, period, and generated state', async () => {
    getDetailMock.mockResolvedValue(detail)
    searchMock.mockResolvedValue({ items: [{ contractNumber: '454890CD' }], page: 1, pageSize: 10, total: 1 })
    getAmortizationMock.mockResolvedValue(scheduleWithDownPayment)

    renderAndSubmit()

    expect(await screen.findByRole('heading', { name: 'Tabla de amortización' })).toBeTruthy()
    expect(screen.getByText('Versión vigente: 7')).toBeTruthy()
    expect(screen.getByRole('heading', { name: 'Enganche' })).toBeTruthy()
    expect(screen.getAllByText('Generada').length).toBeGreaterThanOrEqual(1)
    expect(screen.getByText('$1,500.00')).toBeTruthy()
    expect(screen.getByText('$1,580.00')).toBeTruthy()
    expect(screen.queryByText('$1,580.00 / $1,580.00')).toBeNull()
    expect(screen.getByText(/01\/02\/2026/)).toBeTruthy()
    expect(screen.getByRole('table')).toBeTruthy()
  })

  it('renders a missing down payment and pending state without persisting data', async () => {
    getDetailMock.mockResolvedValue(detail)
    searchMock.mockResolvedValue({ items: [{ contractNumber: '454890CD' }], page: 1, pageSize: 10, total: 1 })
    getAmortizationMock.mockResolvedValue({ ...scheduleWithDownPayment, downPayment: null, payments: [{ ...scheduleWithDownPayment.payments[0], status: 'Pending' }] })

    renderAndSubmit()

    expect(await screen.findByRole('heading', { name: 'Tabla de amortización' })).toBeTruthy()
    expect(screen.queryByRole('heading', { name: 'Enganche' })).toBeNull()
    expect(screen.getByText('Pendiente')).toBeTruthy()
  })

  it('shows a non-disclosing message when the contract is outside the permitted scope', async () => {
    getDetailMock.mockRejectedValue(new ApiError(404))
    searchMock.mockResolvedValue({ items: [], page: 1, pageSize: 10, total: 0 })

    renderAndSubmit()

    expect((await screen.findByRole('alert')).textContent).toContain('No se encontró el contrato dentro del alcance permitido.')
    expect(screen.queryByText('Cliente de prueba')).toBeNull()
  })

  it('ends the session when the amortization request is unauthorized', async () => {
    getDetailMock.mockResolvedValue(detail)
    searchMock.mockResolvedValue({ items: [{ contractNumber: '454890CD' }], page: 1, pageSize: 10, total: 1 })
    getAmortizationMock.mockRejectedValue(new ApiError(401))

    renderAndSubmit()

    await waitFor(() => expect(onUnauthorized).toHaveBeenCalledOnce())
    expect(screen.queryByText('Cliente de prueba')).toBeNull()
  })

  it('shows the forbidden message when amortization access is denied', async () => {
    getDetailMock.mockResolvedValue(detail)
    searchMock.mockResolvedValue({ items: [{ contractNumber: '454890CD' }], page: 1, pageSize: 10, total: 1 })
    getAmortizationMock.mockRejectedValue(new ApiError(403))

    renderAndSubmit()

    await waitFor(() => expect(screen.getByRole('alert').textContent).toContain('No tienes autorización'))
    expect(screen.queryByText('Cliente de prueba')).toBeNull()
  })

  it('shows a generic error and does not persist contract data', async () => {
    const setItem = vi.fn()
    vi.stubGlobal('localStorage', { setItem })
    vi.stubGlobal('sessionStorage', { setItem })
    getDetailMock.mockRejectedValue(new ApiError(500))
    searchMock.mockResolvedValue({ items: [], page: 1, pageSize: 10, total: 0 })

    renderAndSubmit()

    await waitFor(() => expect(screen.getByRole('alert').textContent).toContain('El servicio no está disponible en este momento.'))
    expect(setItem).not.toHaveBeenCalled()
  })
})
