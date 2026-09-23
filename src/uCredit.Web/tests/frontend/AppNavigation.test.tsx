import React from 'react'
import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import App from '../../src/App'
import type { AuthSession } from '../../src/features/auth/authApi'
import type { AuthStatus } from '../../src/features/auth/useAuthSession'

type MockAuthState = {
  session: AuthSession | null
  status: AuthStatus
  error: string
  busy: boolean
  login: (...args: never[]) => Promise<void>
  selectTenant: (...args: never[]) => Promise<void>
  logout: () => Promise<void>
  refresh: () => Promise<AuthSession | null>
  handleUnauthorized: () => void
}

const mocks = vi.hoisted(() => ({
  auth: null as MockAuthState | null,
  refreshBranding: vi.fn(),
  resetBranding: vi.fn(),
}))

vi.mock('../../src/shared/branding/BrandingProvider', () => ({
  useBranding: () => ({
    theme: {
      tenantCode: 'TOYOTA',
      productName: 'uCredit-auto',
      customerName: 'Toyota Financial Services',
      logoUrl: null,
      primaryColor: '#EB0A1E',
      secondaryColor: '#1D1D1F',
      accentColor: '#EB0A1E',
      navigationColor: '#1D1D1F',
      surfaceColor: '#FFFFFF',
      textColor: '#101828',
      fontFamily: 'Inter, system-ui, sans-serif',
      themeMode: 'light',
      browserTitle: 'uCredit-auto | Toyota Financial Services',
      faviconUrl: null,
    },
    loading: false,
    refresh: mocks.refreshBranding,
    reset: mocks.resetBranding,
  }),
}))

vi.mock('../../src/features/auth/useAuthSession', () => ({
  useAuthSession: () => mocks.auth,
}))

vi.mock('../../src/features/contracts/ContractsView', () => ({
  ContractsView: ({ onCreateContract }: { onCreateContract?: () => void }) => <div data-testid="contracts-view"><button type="button" onClick={onCreateContract}>Capturar contrato desde contratos</button></div>,
}))

vi.mock('../../src/features/contracts/ContractCreateView', () => ({
  ContractCreateView: ({ onBack }: { onBack: () => void }) => <div data-testid="contract-create-view"><h2>Captura preliminar</h2><label>Identificador del cliente<input aria-label="Identificador del cliente" /></label><label>Operación<select aria-label="Operación"><option>Crédito directo</option></select></label><button type="button" disabled>Creación pendiente de configuración</button><button type="button" onClick={onBack}>Volver a contratos</button></div>,
}))

vi.mock('../../src/features/customers/CustomersView', () => ({
  CustomersView: () => <div data-testid="customers-view">CustomersView</div>,
}))

function authenticatedSession(permissions: string[]): MockAuthState {
  return {
    session: {
      userName: 'admin@example.test',
      tenant: { tenantId: 'tenant-1', tenantCode: 'TOYOTA' },
      permissions,
      memberships: [],
    },
    status: 'authenticated',
    error: '',
    busy: false,
    login: async () => undefined,
    selectTenant: async () => undefined,
    logout: async () => undefined,
    refresh: async () => null,
    handleUnauthorized: () => undefined,
  }
}

beforeEach(() => {
  mocks.auth = authenticatedSession(['contracts.read', 'customers.read'])
})

afterEach(() => cleanup())

describe('App navigation', () => {
  it('opens CustomersView and returns to ContractsView without losing branding', () => {
    render(<App />)

    expect(screen.getByTestId('contracts-view')).toBeTruthy()
    expect(screen.getByRole('heading', { name: 'uCredit-auto' })).toBeTruthy()
    expect(screen.queryByText('Arquitectura')).toBeNull()
    expect(screen.queryByText('Acceso')).toBeNull()
    expect(screen.queryByText('Seguridad')).toBeNull()
    expect(screen.queryByText('uCredit-auto · Toyota Financial Services')).toBeNull()

    fireEvent.click(screen.getByRole('button', { name: 'Clientes' }))
    expect(screen.getByTestId('customers-view')).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Clientes' }).getAttribute('aria-current')).toBe('page')

    fireEvent.click(screen.getByRole('button', { name: 'Contratos' }))
    expect(screen.getByTestId('contracts-view')).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Contratos' }).getAttribute('aria-current')).toBe('page')
  })

  it('keeps customers unavailable while exposing contract capture to contracts readers', () => {
    mocks.auth = authenticatedSession(['contracts.read'])
    render(<App />)

    const customers = screen.getByRole('button', { name: 'Clientes' })
    expect(customers).toHaveProperty('disabled', true)
    expect(customers.getAttribute('aria-disabled')).toBe('true')
    expect(screen.getByTestId('contracts-view')).toBeTruthy()

    const contractCapture = screen.getByRole('button', { name: 'Captura de contrato' })
    expect(contractCapture).toHaveProperty('disabled', false)
    expect(contractCapture.getAttribute('aria-disabled')).toBe('false')
  })

  it('opens the capture view from the sidebar and returns to contracts', () => {
    render(<App />)

    fireEvent.click(screen.getByRole('button', { name: 'Captura de contrato' }))
    expect(screen.getByTestId('contract-create-view')).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Captura de contrato' }).getAttribute('aria-current')).toBe('page')
    expect(screen.getByRole('button', { name: 'Captura de contrato' }).className).toContain('active')
    expect(screen.getByRole('button', { name: 'Contratos' }).getAttribute('aria-current')).toBeNull()
    expect(screen.getByRole('heading', { name: 'Captura preliminar' })).toBeTruthy()
    expect(screen.getByLabelText('Identificador del cliente')).toBeTruthy()
    expect(screen.getByLabelText('Operación')).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Creación pendiente de configuración' })).toHaveProperty('disabled', true)

    fireEvent.click(screen.getByRole('button', { name: 'Volver a contratos' }))
    expect(screen.getByTestId('contracts-view')).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Contratos' }).getAttribute('aria-current')).toBe('page')
    expect(screen.getByRole('button', { name: 'Captura de contrato' }).getAttribute('aria-current')).toBeNull()
  })

  it('opens capture from ContractsView without invoking a create endpoint', () => {
    render(<App />)

    fireEvent.click(screen.getByRole('button', { name: 'Capturar contrato desde contratos' }))
    expect(screen.getByTestId('contract-create-view')).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Captura de contrato' }).getAttribute('aria-current')).toBe('page')
    expect(screen.getByRole('button', { name: 'Contratos' }).getAttribute('aria-current')).toBeNull()
  })

  it('does not expose capture to users without contracts.read', () => {
    mocks.auth = authenticatedSession(['customers.read'])
    render(<App />)

    const capture = screen.getByRole('button', { name: 'Captura de contrato' })
    expect(capture).toHaveProperty('disabled', true)
    expect(screen.queryByTestId('contract-create-view')).toBeNull()
  })
})
