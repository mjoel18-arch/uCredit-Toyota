import { useState, type FormEvent } from 'react'
import { ApiError, apiErrorMessage } from '../../shared/api/apiClient'
import { activateCustomerAddress, createCustomerAddress, deactivateCustomerAddress, getCustomerAddresses, getCustomerByPersonId, getCustomerProfileReadiness, searchCustomers, updateCustomerAddress, type CustomerAddressPayload, type CustomerDetail, type CustomerListItem, type CustomerProfileReadiness, type ManagedCustomerAddress } from '../auth/authApi'
import { CustomerCreateView } from './CustomerCreateView'

export function CustomersView({ productName, customerName, onUnauthorized, canCreate = false, onCreated = () => undefined }: { productName: string; customerName: string; onUnauthorized: () => void; canCreate?: boolean; onCreated?: (personId: number, pepValidationStatus?: string) => void }) {
  const [personId, setPersonId] = useState('')
  const [rfc, setRfc] = useState('')
  const [name, setName] = useState('')
  const [items, setItems] = useState<CustomerListItem[]>([])
  const [detail, setDetail] = useState<CustomerDetail | null>(null)
  const [readiness, setReadiness] = useState<CustomerProfileReadiness | null>(null)
  const [addresses, setAddresses] = useState<ManagedCustomerAddress[]>([])
  const [page, setPage] = useState(1)
  const [total, setTotal] = useState(0)
  const [message, setMessage] = useState('')
  const [busy, setBusy] = useState(false)
  const [creating, setCreating] = useState(false)
  const [pepNotice, setPepNotice] = useState(false)
  if (creating) return <CustomerCreateView onBack={() => setCreating(false)} onCreated={(personId, pepValidationStatus) => { setCreating(false); setPepNotice(pepValidationStatus === 'NotExecuted'); onCreated(personId, pepValidationStatus) }} />

  async function submit(event: FormEvent<HTMLFormElement>, requestedPage = 1) {
    event.preventDefault()
    await search(requestedPage)
  }

  async function search(requestedPage = 1) {
    if (!personId.trim() && !rfc.trim() && !name.trim()) {
      setMessage('Captura un identificador, RFC o nombre.')
      return
    }
    setBusy(true); setMessage(''); setDetail(null); setReadiness(null)
    try {
      const result = await searchCustomers({ personId: personId ? Number(personId) : undefined, rfc: rfc.trim() || undefined, name: name.trim() || undefined, page: requestedPage })
      setItems(result.items); setPage(result.page); setTotal(result.total)
    } catch (error) {
      if (error instanceof ApiError && error.status === 401) { onUnauthorized(); return }
      setMessage(error instanceof ApiError && error.status === 404 ? 'No se encontró el cliente.' : apiErrorMessage(error, 'No fue posible consultar los clientes.'))
    } finally { setBusy(false) }
  }

  async function openDetail(item: CustomerListItem) {
    try {
      const [customer, profileReadiness, customerAddresses] = await Promise.all([
        getCustomerByPersonId(item.personId),
        getCustomerProfileReadiness(item.personId),
        getCustomerAddresses(item.personId),
      ])
      setDetail(customer); setReadiness(profileReadiness); setAddresses(customerAddresses); setMessage('')
    }
    catch (error) { if (error instanceof ApiError && error.status === 401) onUnauthorized(); else setMessage(error instanceof ApiError && error.status === 404 ? 'No se encontró el cliente.' : apiErrorMessage(error, 'No fue posible consultar el cliente.')) }
  }

  return <section className="hero-card customer-search" aria-labelledby="customers-title">
    <span className="status-dot" /> Consulta de clientes
    <h2 id="customers-title">{productName} · {customerName || 'Clientes'}</h2>
    <p>Consulta clientes y, si tu sesión está autorizada, inicia un alta controlada.</p>{canCreate && <button type="button" onClick={() => setCreating(true)}>Crear cliente</button>}
    <form className="customer-search-form" onSubmit={(event) => void submit(event)}>
      <label>Identificador<input value={personId} onChange={(event) => setPersonId(event.target.value)} inputMode="numeric" /></label>
      <label>RFC<input value={rfc} onChange={(event) => setRfc(event.target.value)} maxLength={13} /></label>
      <label>Nombre o razón social<input value={name} onChange={(event) => setName(event.target.value)} maxLength={200} /></label>
      <button type="submit" disabled={busy}>{busy ? 'Consultando…' : 'Buscar clientes'}</button>
    </form>
    {pepNotice && <div className="notice notice-warning" role="status">Validación PEP no ejecutada en este ambiente de demostración.</div>}
    {message && <div className="notice notice-error" role="alert">{message}</div>}
    {items.length > 0 && <div className="customer-results" aria-live="polite"><table><thead><tr><th>Identificador</th><th>RFC</th><th>Nombre</th><th>Personalidad</th><th>Estatus</th><th /></tr></thead><tbody>{items.map(item => <tr key={item.personId}><td>{item.personId}</td><td>{item.rfcMasked ?? 'No disponible'}</td><td>{item.name}</td><td>{item.legalPersonality.description ?? item.legalPersonality.code}</td><td>{item.status.description ?? item.status.code}</td><td><button type="button" onClick={() => void openDetail(item)}>Ver detalle</button></td></tr>)}</tbody></table><div className="customer-pagination"><span>{total} resultado(s)</span><button type="button" disabled={busy || page <= 1} onClick={() => void search(page - 1)}>Anterior</button><button type="button" disabled={busy || page * 20 >= total} onClick={() => void search(page + 1)}>Siguiente</button></div></div>}
    {detail && readiness && <CustomerDetailCard detail={detail} readiness={readiness} addresses={addresses} canWrite={canCreate} onUnauthorized={onUnauthorized} onRefresh={async () => { const [loadedAddresses, loadedReadiness] = await Promise.all([getCustomerAddresses(detail.personId), getCustomerProfileReadiness(detail.personId)]); setAddresses(loadedAddresses); setReadiness(loadedReadiness) }} />}
    <div className="customer-upcoming"><button type="button" disabled>Editar cliente · Próximamente</button><button type="button" disabled>Crear propuesta · Próximamente</button><button type="button" disabled aria-disabled="true">Capturar contrato · Próximamente</button></div>
  </section>
}

function CustomerDetailCard({ detail, readiness, addresses, canWrite, onUnauthorized, onRefresh }: { detail: CustomerDetail; readiness: CustomerProfileReadiness; addresses: ManagedCustomerAddress[]; canWrite: boolean; onUnauthorized: () => void; onRefresh: () => Promise<void> }) {
  const requirements = [
    ['Datos generales', readiness.hasGeneralData, 'generalData'],
    ['Domicilio', readiness.hasAddress, 'address'],
    ['Teléfono', readiness.hasPhone, 'phone'],
    ['Cuenta', readiness.hasAccount, 'account'],
  ] as const
  return <article className="contract-detail" aria-labelledby="customer-detail-title"><header className="contract-detail-header"><div><span className="eyebrow">Cliente</span><h3 id="customer-detail-title">{detail.name}</h3></div><span className="detail-match">ID {detail.personId}</span></header><dl className="detail-grid"><Detail label="RFC" value={detail.rfc} /><Detail label="Personalidad" value={detail.legalPersonality.description} /><Detail label="Estatus" value={detail.status.description} /><Detail label="Teléfono principal" value={detail.primaryPhone ? [detail.primaryPhone.areaCode, detail.primaryPhone.phoneNumber].filter(Boolean).join(' ') : null} /><Detail label="Correo" value={detail.activeEmails[0]?.email} /><Detail label="Domicilio" value={detail.primaryAddress ? [detail.primaryAddress.streetAndNumber, detail.primaryAddress.exteriorNumber, detail.primaryAddress.city, detail.primaryAddress.state].filter(Boolean).join(', ') : null} /></dl><h4>Requisitos para contrato</h4><ul className="readiness-requirements">{requirements.map(([label, complete, key]) => <li key={key} data-status={complete ? 'complete' : 'pending'}><span>{label}</span><strong>{complete ? 'Completo' : 'Pendiente'}</strong></li>)}</ul><p role="status">{readiness.canCreateContract ? 'Cliente habilitado para contratos.' : 'Este cliente todavía no puede tener contratos.'}</p><AddressManagement addresses={addresses} personId={detail.personId} canWrite={canWrite} onUnauthorized={onUnauthorized} onRefresh={onRefresh} /><h4>Teléfonos activos</h4><ul>{detail.activePhones.map(phone => <li key={phone.phoneId}>{[phone.areaCode, phone.phoneNumber, phone.extension].filter(Boolean).join(' ')}</li>)}</ul><h4>Correos activos</h4><ul>{detail.activeEmails.map(email => <li key={email.emailId}>{email.email ?? 'No disponible'}</li>)}</ul></article>
}

function AddressManagement({ addresses, personId, canWrite, onUnauthorized, onRefresh }: { addresses: ManagedCustomerAddress[]; personId: number; canWrite: boolean; onUnauthorized: () => void; onRefresh: () => Promise<void> }) {
  const [editing, setEditing] = useState<ManagedCustomerAddress | null>(null)
  const [panelOpen, setPanelOpen] = useState(false)
  const [busy, setBusy] = useState(false)
  const [message, setMessage] = useState('')
  const [pendingDeactivate, setPendingDeactivate] = useState<ManagedCustomerAddress | null>(null)

  async function save(payload: CustomerAddressPayload) {
    setBusy(true); setMessage('')
    try {
      if (editing) await updateCustomerAddress(personId, editing.addressId, { ...payload, expectedModifiedAt: editing.modifiedAt })
      else await createCustomerAddress(personId, payload)
      setPanelOpen(false); setEditing(null); await onRefresh(); setMessage('Domicilio guardado correctamente.')
    } catch (error) { handleAddressError(error, onUnauthorized, setMessage, 'No fue posible guardar el domicilio.') } finally { setBusy(false) }
  }

  async function changeState(address: ManagedCustomerAddress, activate: boolean, replacementAddressId?: number) {
    setBusy(true); setMessage('')
    try {
      const payload = { expectedModifiedAt: address.modifiedAt, ...(replacementAddressId ? { replacementAddressId } : {}) }
      if (activate) await activateCustomerAddress(personId, address.addressId, payload)
      else await deactivateCustomerAddress(personId, address.addressId, payload)
      setPendingDeactivate(null); await onRefresh(); setMessage(activate ? 'Domicilio activado.' : 'Domicilio desactivado.')
    } catch (error) { handleAddressError(error, onUnauthorized, setMessage, 'No fue posible actualizar el estado del domicilio.') } finally { setBusy(false) }
  }

  return <section className="customer-addresses" aria-labelledby="addresses-title"><div className="section-heading"><div><span className="eyebrow">Expediente</span><h4 id="addresses-title">Domicilios</h4></div>{canWrite && <button type="button" onClick={() => { setEditing(null); setPanelOpen(true) }}>Agregar</button>}</div>{message && <div className="notice notice-status" role="status">{message}</div>}{addresses.length === 0 ? <p>No hay domicilios registrados.</p> : <div className="address-list">{addresses.map(address => <article className="address-card" key={address.addressId}><div><strong>{address.addressTypeDescription || `Tipo ${address.addressTypeCode}`}</strong><p>{[address.streetAndNumber, address.exteriorNumber, address.city, address.state].filter(Boolean).join(', ') || 'Domicilio sin descripción'}</p><small>{address.isActive ? 'Activo' : 'Inactivo'} · {address.isDefault ? 'Predeterminado' : 'No predeterminado'} · Usos: {friendlyUses(address.uses)}</small></div>{canWrite && <div className="address-actions"><button type="button" disabled={busy} onClick={() => { setEditing(address); setPanelOpen(true) }}>Editar</button>{address.isActive ? <button type="button" disabled={busy} onClick={() => setPendingDeactivate(address)}>Desactivar</button> : <button type="button" disabled={busy} onClick={() => void changeState(address, true)}>Activar</button>}</div>}</article>)}</div>}{pendingDeactivate && <DeactivatePanel address={pendingDeactivate} addresses={addresses} busy={busy} onCancel={() => setPendingDeactivate(null)} onConfirm={(replacement) => void changeState(pendingDeactivate, false, replacement)} />}{panelOpen && <AddressPanel address={editing} busy={busy} onCancel={() => { setPanelOpen(false); setEditing(null) }} onSave={(payload) => void save(payload)} />}</section>
}

function AddressPanel({ address, busy, onCancel, onSave }: { address: ManagedCustomerAddress | null; busy: boolean; onCancel: () => void; onSave: (payload: CustomerAddressPayload) => void }) {
  const [type, setType] = useState(address?.addressTypeCode ?? 1)
  const [uses, setUses] = useState<string[]>(address?.uses ?? defaultUses(1))
  const [isDefault, setIsDefault] = useState(address?.isDefault ?? false)
  const [form, setForm] = useState({ postalCode: address?.postalCode ?? '', state: address?.state ?? '', municipality: address?.municipality ?? '', city: address?.city ?? '', neighborhood: address?.neighborhood ?? '', streetAndNumber: address?.streetAndNumber ?? '', exteriorNumber: address?.exteriorNumber ?? '', interiorNumber: address?.interiorNumber ?? '', reference: '', schedule: '', countryCode: String(address?.countryCode ?? 1) })
  const [error, setError] = useState('')

  function changeType(value: number) { setType(value); setUses(defaultUses(value)); if (value === 1) setIsDefault(true) }
  function toggleUse(use: string) { setUses(current => current.includes(use) ? current.filter(item => item !== use) : [...current, use]) }
  function submit(event: FormEvent<HTMLFormElement>) { event.preventDefault(); if (!form.postalCode.trim() || !form.state.trim() || !form.city.trim() || !form.streetAndNumber.trim() || !form.exteriorNumber.trim()) { setError('Completa los campos obligatorios del domicilio.'); return } onSave({ ...form, addressTypeCode: type, uses, isDefault, countryCode: Number(form.countryCode) }) }
  const useOptions = [['statements', 'Estado de cuenta'], ['other', 'Otros']] as const
  return <div className="side-panel-backdrop"><aside className="side-panel" role="dialog" aria-modal="true" aria-labelledby="address-panel-title"><h4 id="address-panel-title">{address ? 'Editar domicilio' : 'Agregar domicilio'}</h4><form onSubmit={submit}><label>Tipo de domicilio<select value={type} onChange={event => changeType(Number(event.target.value))}><option value="1">Dirección única</option><option value="2">Dirección fiscal</option><option value="3">Dirección administrativa</option><option value="4">Dirección social</option></select></label><fieldset><legend>Usos del domicilio</legend><label><input type="checkbox" checked={type < 3} disabled />Facturación</label>{useOptions.map(([value, label]) => <label key={value}><input type="checkbox" checked={uses.includes(value)} disabled={type === 1} onChange={() => toggleUse(value)} />{label}</label>)}</fieldset><AddressInput label="Código postal" value={form.postalCode} onChange={value => setForm({ ...form, postalCode: value })} required /><AddressInput label="Estado" value={form.state} onChange={value => setForm({ ...form, state: value })} required /><AddressInput label="Ciudad" value={form.city} onChange={value => setForm({ ...form, city: value })} required /><AddressInput label="Municipio" value={form.municipality} onChange={value => setForm({ ...form, municipality: value })} required /><AddressInput label="Colonia" value={form.neighborhood} onChange={value => setForm({ ...form, neighborhood: value })} required /><AddressInput label="Calle y número" value={form.streetAndNumber} onChange={value => setForm({ ...form, streetAndNumber: value })} required /><AddressInput label="Número exterior" value={form.exteriorNumber} onChange={value => setForm({ ...form, exteriorNumber: value })} required /><AddressInput label="Número interior" value={form.interiorNumber} onChange={value => setForm({ ...form, interiorNumber: value })} /><label><input type="checkbox" checked={isDefault} disabled={Boolean(address?.isDefault) || type === 1 && !address} onChange={event => setIsDefault(event.target.checked)} />Predeterminado</label>{error && <div className="notice notice-error" role="alert">{error}</div>}<div className="side-panel-actions"><button type="button" onClick={onCancel}>Cancelar</button><button type="submit" disabled={busy}>{busy ? 'Guardando…' : 'Guardar domicilio'}</button></div></form></aside></div>
}

function DeactivatePanel({ address, addresses, busy, onCancel, onConfirm }: { address: ManagedCustomerAddress; addresses: ManagedCustomerAddress[]; busy: boolean; onCancel: () => void; onConfirm: (replacement?: number) => void }) {
  const replacements = addresses.filter(item => item.addressId !== address.addressId && item.isActive)
  const [replacement, setReplacement] = useState('')
  return <div className="side-panel-backdrop"><aside className="side-panel" role="dialog" aria-modal="true" aria-labelledby="deactivate-title"><h4 id="deactivate-title">Desactivar domicilio</h4><p>Esta acción conserva el registro y lo marca como inactivo.</p>{address.isDefault && <label>Domicilio activo de reemplazo<select value={replacement} onChange={event => setReplacement(event.target.value)} required><option value="">Selecciona un reemplazo</option>{replacements.map(item => <option key={item.addressId} value={item.addressId}>{item.addressTypeDescription || `Domicilio ${item.addressId}`}</option>)}</select></label>}<div className="side-panel-actions"><button type="button" onClick={onCancel}>Cancelar</button><button type="button" disabled={busy || address.isDefault && !replacement} onClick={() => onConfirm(replacement ? Number(replacement) : undefined)}>Confirmar desactivación</button></div></aside></div>
}

function AddressInput({ label, value, onChange, required = false }: { label: string; value: string; onChange: (value: string) => void; required?: boolean }) { return <label>{label}<input value={value} required={required} onChange={event => onChange(event.target.value)} /></label> }
function defaultUses(type: number): string[] { return type === 1 ? ['billing', 'statements', 'other'] : type === 2 ? ['billing'] : [] }
function friendlyUses(uses: string[]): string { return uses.map(use => use === 'billing' ? 'Facturación' : use === 'statements' ? 'Estado de cuenta' : 'Otros').join(', ') || 'Ninguno' }
function handleAddressError(error: unknown, onUnauthorized: () => void, setMessage: (message: string) => void, fallback: string) { if (error instanceof ApiError && error.status === 401) { onUnauthorized(); return } if (error instanceof ApiError && error.status === 409) { setMessage(error.code === 'address_default_required' ? 'Selecciona un domicilio activo de reemplazo.' : 'El domicilio cambió; vuelve a cargarlo antes de guardar.') } else setMessage(error instanceof ApiError ? apiErrorMessage(error, fallback) : fallback) }
function Detail({ label, value }: { label: string; value: string | null | undefined }) { return <div className="detail-field"><dt>{label}</dt><dd>{value || 'No disponible'}</dd></div> }
