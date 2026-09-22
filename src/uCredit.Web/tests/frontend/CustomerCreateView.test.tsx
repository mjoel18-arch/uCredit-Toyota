import React from 'react'
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { createCustomer, getCustomerRoleCatalog } from '../../src/features/auth/authApi'
import { CustomerCreateView, getTemporaryTaxRegimeCode } from '../../src/features/customers/CustomerCreateView'
import { ApiError } from '../../src/shared/api/apiClient'

vi.mock('../../src/features/auth/authApi', async () => {
  const actual = await vi.importActual<typeof import('../../src/features/auth/authApi')>('../../src/features/auth/authApi')
  return { ...actual, createCustomer: vi.fn(), getCustomerRoleCatalog: vi.fn() }
})

const createMock = vi.mocked(createCustomer)
const roleCatalogMock = vi.mocked(getCustomerRoleCatalog)

afterEach(() => {
  cleanup()
  createMock.mockReset()
  roleCatalogMock.mockReset()
})

const activeRoles = [
  { roleCode: 1, roleName: 'Cliente' },
  { roleCode: 3, roleName: 'Apoderado' },
  { roleCode: 25, roleName: 'Accionista' },
]

describe('CustomerCreateView', () => {
  it('maps the temporary tax regime by legal personality', () => {
    expect(getTemporaryTaxRegimeCode(1)).toBe('605')
    expect(getTemporaryTaxRegimeCode(2)).toBe('612')
    expect(getTemporaryTaxRegimeCode(20)).toBe('601')
  })

  it('keeps the moral personality disabled until its capital regime catalog is confirmed', () => {
    roleCatalogMock.mockResolvedValue(activeRoles)
    render(<CustomerCreateView onBack={vi.fn()} onCreated={vi.fn()} />)
    const moralOption = screen.getByRole('option', {
      name: 'Moral (Próximamente)',
    }) as HTMLOptionElement

    expect(moralOption.disabled).toBe(true)
  })

  it('sends the temporary tax regime selected by personality', async () => {
    roleCatalogMock.mockResolvedValue(activeRoles)
    createMock.mockResolvedValue({ personId: 42, pepValidationStatus: 'NotExecuted' })
    render(<CustomerCreateView onBack={vi.fn()} onCreated={vi.fn()} />)
    await screen.findByLabelText('Cliente')
    fireEvent.change(screen.getByLabelText('Personalidad'), { target: { value: '2' } })
    fireEvent.click(screen.getByLabelText('Cliente'))
    fireEvent.submit(document.querySelector('form.customer-create-form') as HTMLFormElement)
    await waitFor(() => expect(createMock).toHaveBeenCalledTimes(1))
    expect(createMock.mock.calls[0][0].taxRegimeCode).toBe('612')
  })

  it('creates the person without domicile fields in the form or payload', async () => {
    roleCatalogMock.mockResolvedValue(activeRoles)
    createMock.mockResolvedValue({ personId: 42, pepValidationStatus: 'NotExecuted' })
    render(<CustomerCreateView onBack={vi.fn()} onCreated={vi.fn()} />)
    await screen.findByLabelText('Cliente')

    expect(screen.queryByLabelText('Código postal')).toBeNull()
    expect(screen.queryByLabelText('Estado')).toBeNull()
    expect(screen.queryByLabelText('Municipio')).toBeNull()
    expect(screen.queryByLabelText('Tipo de domicilio')).toBeNull()

    fireEvent.click(screen.getByLabelText('Cliente'))
    fireEvent.submit(document.querySelector('form.customer-create-form') as HTMLFormElement)
    await waitFor(() => expect(createMock).toHaveBeenCalledTimes(1))
    const payload = createMock.mock.calls[0][0] as unknown as Record<string, unknown>
    expect(Object.keys(payload).some(key => key.toLowerCase().includes('address') || key.toLowerCase().includes('postal') || key === 'state' || key === 'city')).toBe(false)
  })

  it('does not render or send initial email fields', async () => {
    roleCatalogMock.mockResolvedValue(activeRoles)
    createMock.mockResolvedValue({ personId: 42, pepValidationStatus: 'NotExecuted' })
    render(<CustomerCreateView onBack={vi.fn()} onCreated={vi.fn()} />)
    await screen.findByLabelText('Cliente')

    expect(screen.queryByLabelText('Contacto del correo')).toBeNull()
    expect(screen.queryByLabelText('Correo electrónico')).toBeNull()
    expect(screen.queryByText('Usos del correo')).toBeNull()

    fireEvent.click(screen.getByLabelText('Cliente'))
    fireEvent.submit(document.querySelector('form.customer-create-form') as HTMLFormElement)
    await waitFor(() => expect(createMock).toHaveBeenCalledTimes(1))
    const payload = createMock.mock.calls[0][0] as unknown as Record<string, unknown>
    expect(payload).not.toHaveProperty('email')
    expect(payload).not.toHaveProperty('emailContact')
    expect(payload).not.toHaveProperty('emailUsageCodes')
  })

  it('asks for PEP confirmation after the controlled 422 and retries only once', async () => {
    roleCatalogMock.mockResolvedValue(activeRoles)
    createMock.mockRejectedValueOnce(new ApiError(422, 'pep_confirmation_required')).mockResolvedValueOnce({ personId: 42, pepValidationStatus: 'Executed' })
    const onCreated = vi.fn()
    render(<CustomerCreateView onBack={vi.fn()} onCreated={onCreated} />)
    await screen.findByLabelText('Cliente')

    fireEvent.click(screen.getByLabelText('Cliente'))
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

  it('shows the duplicate RFC message for 409 without retrying automatically', async () => {
    roleCatalogMock.mockResolvedValue(activeRoles)
    createMock.mockRejectedValueOnce(new ApiError(409))
    render(<CustomerCreateView onBack={vi.fn()} onCreated={vi.fn()} />)
    await screen.findByLabelText('Cliente')

    fireEvent.click(screen.getByLabelText('Cliente'))
    fireEvent.submit(document.querySelector('form.customer-create-form') as HTMLFormElement)
    const alert = await screen.findByRole('alert')
    expect(alert.textContent).toContain('Ya existe un cliente registrado con ese RFC; no se guardó información.')
    expect(createMock).toHaveBeenCalledTimes(1)
    expect(alert.textContent).not.toContain('AAA')
  })

  it('renders active roles without requiring Cliente', async () => {
    roleCatalogMock.mockResolvedValue(activeRoles)
    render(<CustomerCreateView onBack={vi.fn()} onCreated={vi.fn()} />)
    const customer = await screen.findByLabelText('Cliente')
    expect((customer as HTMLInputElement).checked).toBe(false)
    expect(screen.getByLabelText('Apoderado')).toBeTruthy()
    expect(screen.getByLabelText('Accionista')).toBeTruthy()
  })

  it('requires at least one role and focuses the role group', async () => {
    roleCatalogMock.mockResolvedValue(activeRoles)
    render(<CustomerCreateView onBack={vi.fn()} onCreated={vi.fn()} />)
    await screen.findByLabelText('Cliente')
    fireEvent.submit(document.querySelector('form.customer-create-form') as HTMLFormElement)
    const alert = await screen.findByRole('alert')
    expect(alert.textContent).toContain('Selecciona al menos un rol.')
    expect(document.activeElement?.tagName).toBe('FIELDSET')
    expect(createMock).not.toHaveBeenCalled()
  })

  it('sends multiple selected dynamic role codes and no phone fields', async () => {
    roleCatalogMock.mockResolvedValue(activeRoles)
    createMock.mockResolvedValue({ personId: 42, pepValidationStatus: 'NotExecuted' })
    render(<CustomerCreateView onBack={vi.fn()} onCreated={vi.fn()} />)
    await screen.findByLabelText('Cliente')
    fireEvent.click(screen.getByLabelText('Apoderado'))
    fireEvent.click(screen.getByLabelText('Accionista'))
    fireEvent.submit(document.querySelector('form.customer-create-form') as HTMLFormElement)
    await waitFor(() => expect(createMock).toHaveBeenCalledTimes(1))
    expect(createMock.mock.calls[0][0].roleCodes).toEqual([3, 25])
    expect(createMock.mock.calls[0][0]).not.toHaveProperty('phoneNumber')
    expect(createMock.mock.calls[0][0]).not.toHaveProperty('areaCode')
  })

  it('shows a role catalog error and keeps creation disabled', async () => {
    roleCatalogMock.mockRejectedValue(new Error('catalog unavailable'))
    render(<CustomerCreateView onBack={vi.fn()} onCreated={vi.fn()} />)
    expect(await screen.findByRole('alert')).toBeTruthy()
    expect((screen.getByRole('button', { name: 'Revisar y crear cliente' }) as HTMLButtonElement).disabled).toBe(true)
  })
})
