import React from 'react'
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { createCustomer } from '../../src/features/auth/authApi'
import { CustomerCreateView } from '../../src/features/customers/CustomerCreateView'
import { ApiError } from '../../src/shared/api/apiClient'

vi.mock('../../src/features/auth/authApi', async () => {
  const actual = await vi.importActual<typeof import('../../src/features/auth/authApi')>('../../src/features/auth/authApi')
  return { ...actual, createCustomer: vi.fn() }
})

const createMock = vi.mocked(createCustomer)

afterEach(() => {
  cleanup()
  createMock.mockReset()
})

describe('CustomerCreateView email usages', () => {
  it('selects invoice delivery initially', () => {
    render(<CustomerCreateView onBack={vi.fn()} onCreated={vi.fn()} />)

    expect(screen.getByLabelText('Envío de facturas')).toHaveProperty('checked', true)
    expect(screen.getByLabelText('Envío de estado de cuenta')).toHaveProperty('checked', false)
    expect(screen.getByLabelText('Salesforce')).toHaveProperty('checked', false)
  })

  it('does not allow removing the last usage and announces the error', () => {
    render(<CustomerCreateView onBack={vi.fn()} onCreated={vi.fn()} />)

    fireEvent.click(screen.getByLabelText('Envío de facturas'))

    expect(screen.getByLabelText('Envío de facturas')).toHaveProperty('checked', true)
    expect(screen.getByRole('alert').textContent).toContain('al menos un uso')
    expect(document.activeElement).toBe(screen.getByRole('group'))
    expect(createMock).not.toHaveBeenCalled()
  })

  it('supports multiple usages', () => {
    render(<CustomerCreateView onBack={vi.fn()} onCreated={vi.fn()} />)

    fireEvent.click(screen.getByLabelText('Envío de estado de cuenta'))
    fireEvent.click(screen.getByLabelText('Salesforce'))

    expect(screen.getByLabelText('Envío de facturas')).toHaveProperty('checked', true)
    expect(screen.getByLabelText('Envío de estado de cuenta')).toHaveProperty('checked', true)
    expect(screen.getByLabelText('Salesforce')).toHaveProperty('checked', true)
  })

  it('asks for PEP confirmation after the controlled 422 and retries only once', async () => {
    createMock.mockRejectedValueOnce(new ApiError(422, 'pep_confirmation_required')).mockResolvedValueOnce({ personId: 42, pepValidationStatus: 'Executed' })
    const onCreated = vi.fn()
    render(<CustomerCreateView onBack={vi.fn()} onCreated={onCreated} />)

    fireEvent.submit(document.querySelector('form.customer-create-form') as HTMLFormElement)
    await waitFor(() => expect(screen.getByText(/coincidencia PEP/)).toBeTruthy())
    const confirmation = screen.getByLabelText(/Confirmo que deseo continuar/)
    fireEvent.click(confirmation)
    fireEvent.submit(document.querySelector('form.customer-create-form') as HTMLFormElement)
    await waitFor(() => expect(createMock).toHaveBeenCalledTimes(2))
    expect(createMock.mock.calls[1][0].pepConfirmed).toBe(true)
    expect(onCreated).toHaveBeenCalledWith(42, 'Executed')
    fireEvent.submit(document.querySelector('form.customer-create-form') as HTMLFormElement)
    expect(createMock).toHaveBeenCalledTimes(2)
  })
})
