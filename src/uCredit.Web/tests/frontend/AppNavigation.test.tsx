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
  ContractsView: () => <div data-testid="contracts-view">ContractsView</div>,
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

  it('keeps contract capture disabled and prevents customers without permission', () => {
    mocks.auth = authenticatedSession(['contracts.read'])
    render(<App />)

    const customers = screen.getByRole('button', { name: 'Clientes' })
    expect(customers).toHaveProperty('disabled', true)
    expect(customers.getAttribute('aria-disabled')).toBe('true')
    expect(screen.getByTestId('contracts-view')).toBeTruthy()

    const contractCapture = screen.getByRole('button', { name: 'Captura de contrato · Próximamente' })
    expect(contractCapture).toHaveProperty('disabled', true)
    expect(contractCapture.getAttribute('aria-disabled')).toBe('true')
  })
})
