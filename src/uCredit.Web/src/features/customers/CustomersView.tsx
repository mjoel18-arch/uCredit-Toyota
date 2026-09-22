import { useEffect, useState, type FormEvent } from 'react'
import { ApiError, apiErrorMessage } from '../../shared/api/apiClient'
import { activateCustomerAccount, activateCustomerAddress, activateCustomerEmail, activateCustomerPhone, createCustomerAccount, createCustomerAddress, createCustomerEmail, createCustomerPhone, deactivateCustomerAccount, deactivateCustomerAddress, deactivateCustomerEmail, deactivateCustomerPhone, getCustomerAccounts, getCustomerAddresses, getCustomerBanks, getCustomerByPersonId, getCustomerEmailUsages, getCustomerEmails, getCustomerPhones, getCustomerProfileReadiness, searchCustomers, updateCustomerAccount, updateCustomerAddress, updateCustomerEmail, updateCustomerPhone, type CustomerAccountPayload, type CustomerAddressPayload, type CustomerDetail, type CustomerEmailPayload, type CustomerEmailUsage, type CustomerListItem, type CustomerPhonePayload, type CustomerProfileReadiness, type ManagedCustomerAccount, type ManagedCustomerAddress, type ManagedCustomerEmail, type ManagedCustomerPhone, type CustomerBank } from '../auth/authApi'
import { CustomerCreateView } from './CustomerCreateView'

export function CustomersView({ onUnauthorized, canCreate = false, onCreated = () => undefined }: { productName: string; customerName: string; onUnauthorized: () => void; canCreate?: boolean; onCreated?: (personId: number, pepValidationStatus?: string) => void }) {
  const [personId, setPersonId] = useState('')
  const [rfc, setRfc] = useState('')
  const [name, setName] = useState('')
  const [items, setItems] = useState<CustomerListItem[]>([])
  const [detail, setDetail] = useState<CustomerDetail | null>(null)
  const [readiness, setReadiness] = useState<CustomerProfileReadiness | null>(null)
  const [addresses, setAddresses] = useState<ManagedCustomerAddress[]>([])
  const [phones, setPhones] = useState<ManagedCustomerPhone[]>([])
  const [accounts, setAccounts] = useState<ManagedCustomerAccount[]>([])
  const [emails, setEmails] = useState<ManagedCustomerEmail[]>([])
  const [page, setPage] = useState(1)
  const [total, setTotal] = useState(0)
  const [message, setMessage] = useState('')
  const [busy, setBusy] = useState(false)
  const [creating, setCreating] = useState(false)
  const [pepNotice, setPepNotice] = useState(false)
  if (creating) return <CustomerCreateView onBack={() => setCreating(false)} onCreated={(createdPersonId, pepValidationStatus) => { setCreating(false); setPepNotice(pepValidationStatus === 'NotExecuted'); setPersonId(String(createdPersonId)); setRfc(''); setName(''); setItems([]); void loadDetail(createdPersonId); onCreated(createdPersonId, pepValidationStatus) }} />

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

  async function loadDetail(personIdToLoad: number) {
    try {
      const [customer, profileReadiness, customerAddresses, customerPhones, customerAccounts, customerEmails] = await Promise.all([
        getCustomerByPersonId(personIdToLoad),
        getCustomerProfileReadiness(personIdToLoad),
        getCustomerAddresses(personIdToLoad),
        getCustomerPhones(personIdToLoad),
        getCustomerAccounts(personIdToLoad).catch(error => { if (error instanceof ApiError && error.status === 401) onUnauthorized(); return [] }),
        getCustomerEmails(personIdToLoad),
      ])
      setDetail(customer); setReadiness(profileReadiness); setAddresses(customerAddresses); setPhones(customerPhones); setAccounts(customerAccounts); setEmails(customerEmails); setMessage('')
    }
    catch (error) { if (error instanceof ApiError && error.status === 401) onUnauthorized(); else setMessage(error instanceof ApiError && error.status === 404 ? 'No se encontró el cliente.' : apiErrorMessage(error, 'No fue posible consultar el cliente.')) }
  }

  async function openDetail(item: CustomerListItem) {
    await loadDetail(item.personId)
  }

  return <section className="hero-card customer-search" aria-labelledby="customers-title">
    <span className="status-dot" /> Consulta de clientes
    <h2 id="customers-title">Clientes</h2>
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
    {detail && readiness && <CustomerDetailCard detail={detail} readiness={readiness} addresses={addresses} phones={phones} accounts={accounts} emails={emails} canWrite={canCreate} onUnauthorized={onUnauthorized} onRefresh={async () => { const [loadedAddresses, loadedReadiness, loadedPhones, loadedAccounts, loadedEmails] = await Promise.all([getCustomerAddresses(detail.personId), getCustomerProfileReadiness(detail.personId), getCustomerPhones(detail.personId), getCustomerAccounts(detail.personId).catch(error => { if (error instanceof ApiError && error.status === 401) onUnauthorized(); return [] }), getCustomerEmails(detail.personId)]); setAddresses(loadedAddresses); setReadiness(loadedReadiness); setPhones(loadedPhones); setAccounts(loadedAccounts); setEmails(loadedEmails) }} />}
    <div className="customer-upcoming"><button type="button" disabled>Editar cliente · Próximamente</button><button type="button" disabled>Crear propuesta · Próximamente</button><button type="button" disabled={!readiness?.canCreateContract} aria-disabled={!readiness?.canCreateContract}>{readiness?.canCreateContract ? 'Capturar contrato' : 'Capturar contrato · Próximamente'}</button></div>
  </section>
}

function CustomerDetailCard({ detail, readiness, addresses, phones, accounts, emails, canWrite, onUnauthorized, onRefresh }: { detail: CustomerDetail; readiness: CustomerProfileReadiness; addresses: ManagedCustomerAddress[]; phones: ManagedCustomerPhone[]; accounts: ManagedCustomerAccount[]; emails: ManagedCustomerEmail[]; canWrite: boolean; onUnauthorized: () => void; onRefresh: () => Promise<void> }) {
  const requirements = [
    ['Datos generales', readiness.hasGeneralData, 'generalData'],
    ['Domicilio', readiness.hasAddress, 'address'],
    ['Teléfono', readiness.hasPhone, 'phone'],
    ['Cuenta', readiness.hasAccount, 'account'],
  ] as const
  return <article className="contract-detail" aria-labelledby="customer-detail-title"><header className="contract-detail-header"><div><span className="eyebrow">Cliente</span><h3 id="customer-detail-title">{detail.name}</h3></div><span className="detail-match">ID {detail.personId}</span></header><dl className="detail-grid"><Detail label="RFC" value={detail.rfc} /><Detail label="Personalidad" value={detail.legalPersonality.description} /><Detail label="Estatus" value={detail.status.description} /><Detail label="Teléfono principal" value={detail.primaryPhone ? [detail.primaryPhone.areaCode, detail.primaryPhone.phoneNumber].filter(Boolean).join(' ') : null} /><Detail label="Correo" value={detail.activeEmails[0]?.email} /><Detail label="Domicilio" value={detail.primaryAddress ? [detail.primaryAddress.streetAndNumber, detail.primaryAddress.exteriorNumber, detail.primaryAddress.city, detail.primaryAddress.state].filter(Boolean).join(', ') : null} /></dl><h4>Requisitos para contrato</h4><ul className="readiness-requirements">{requirements.map(([label, complete, key]) => <li key={key} data-status={complete ? 'complete' : 'pending'}><span>{label}</span><strong>{complete ? 'Completo' : 'Pendiente'}</strong></li>)}</ul><p role="status">{readiness.canCreateContract ? 'Cliente habilitado para contratos.' : 'Este cliente todavía no puede tener contratos.'}</p><AddressManagement addresses={addresses} personId={detail.personId} canWrite={canWrite} onUnauthorized={onUnauthorized} onRefresh={onRefresh} /><PhoneManagement phones={phones} personId={detail.personId} canWrite={canWrite} onUnauthorized={onUnauthorized} onRefresh={onRefresh} /><AccountManagement accounts={accounts} personId={detail.personId} canWrite={canWrite} onUnauthorized={onUnauthorized} onRefresh={onRefresh} /><EmailManagement emails={emails} personId={detail.personId} canWrite={canWrite} onUnauthorized={onUnauthorized} onRefresh={onRefresh} /></article>
}

function EmailManagement({ emails, personId, canWrite, onUnauthorized, onRefresh }: { emails: ManagedCustomerEmail[]; personId: number; canWrite: boolean; onUnauthorized: () => void; onRefresh: () => Promise<void> }) {
  const [editing, setEditing] = useState<ManagedCustomerEmail | null>(null)
  const [open, setOpen] = useState(false)
  const [busy, setBusy] = useState(false)
  const [message, setMessage] = useState('')
  const [usages, setUsages] = useState<CustomerEmailUsage[]>([])
  const [catalogError, setCatalogError] = useState('')

  useEffect(() => {
    void getCustomerEmailUsages().then(setUsages).catch(error => { if (error instanceof ApiError && error.status === 401) onUnauthorized(); else setCatalogError('No fue posible cargar los usos de correo.') })
  }, [onUnauthorized])

  async function openPanel(email: ManagedCustomerEmail | null) {
    setEditing(email); setMessage(''); setCatalogError(''); setOpen(true)
    try { setUsages(await getCustomerEmailUsages()) }
    catch (error) { if (error instanceof ApiError && error.status === 401) onUnauthorized(); else setCatalogError('No fue posible cargar los usos de correo.') }
  }
  async function save(payload: CustomerEmailPayload) {
    setBusy(true); setMessage('')
    try {
      if (editing) await updateCustomerEmail(personId, editing.emailId, { ...payload, expectedModifiedAt: editing.modifiedAt })
      else await createCustomerEmail(personId, payload)
      setOpen(false); setEditing(null); await onRefresh(); setMessage('Correo guardado correctamente.')
    } catch (error) { handleEmailError(error, onUnauthorized, setMessage, 'No fue posible guardar el correo.') }
    finally { setBusy(false) }
  }
  async function changeState(email: ManagedCustomerEmail, activate: boolean) {
    if (!activate && !window.confirm('¿Deseas desactivar este correo? El registro se conservará.')) return
    setBusy(true); setMessage('')
    try {
      if (activate) await activateCustomerEmail(personId, email.emailId, { expectedModifiedAt: email.modifiedAt })
      else await deactivateCustomerEmail(personId, email.emailId, { expectedModifiedAt: email.modifiedAt })
      await onRefresh(); setMessage(activate ? 'Correo activado.' : 'Correo desactivado.')
    } catch (error) { handleEmailError(error, onUnauthorized, setMessage, 'No fue posible actualizar el correo.') }
    finally { setBusy(false) }
  }
  return <section className="customer-emails" aria-labelledby="emails-title"><div className="section-heading"><div><span className="eyebrow">Expediente</span><h4 id="emails-title">Correos</h4></div>{canWrite && <button type="button" onClick={() => void openPanel(null)}>Agregar</button>}</div>{message && <div className="notice notice-status" role="status">{message}</div>}{emails.length === 0 ? <p>No hay correos registrados.</p> : <div className="email-list">{emails.map(email => <article className="email-card" key={email.emailId}><div><strong>{email.status === 'Active' ? 'Activo' : 'Inactivo'}</strong><p>{email.status === 'Active' ? email.email : 'Correo inactivo'}</p><small>{email.contact || 'Sin contacto'} · Usos: {friendlyEmailUses(email.usageCodes, usages)}</small></div>{canWrite && <div className="email-actions"><button type="button" disabled={busy} onClick={() => void openPanel(email)}>Editar</button>{email.status === 'Active' ? <button type="button" disabled={busy} onClick={() => void changeState(email, false)}>Desactivar</button> : <button type="button" disabled={busy} onClick={() => void changeState(email, true)}>Activar</button>}</div>}</article>)}</div>}{open && <EmailPanel email={editing} usages={usages} catalogError={catalogError} busy={busy} onCancel={() => { setOpen(false); setEditing(null) }} onSave={(payload) => void save(payload)} />}</section>
}

function EmailPanel({ email, usages, catalogError, busy, onCancel, onSave }: { email: ManagedCustomerEmail | null; usages: CustomerEmailUsage[]; catalogError: string; busy: boolean; onCancel: () => void; onSave: (payload: CustomerEmailPayload) => void }) {
  const [address, setAddress] = useState('')
  const [contact, setContact] = useState(email?.contact ?? '')
  const defaultUsageCode = usages.find(usage => usage.description.toLocaleLowerCase().includes('factura'))?.code ?? usages[0]?.code
  const [selected, setSelected] = useState<number[]>(email?.usageCodes ?? (defaultUsageCode === undefined ? [] : [defaultUsageCode]))
  const [error, setError] = useState('')
  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!email && !address.trim()) { setError('Captura un correo electrónico.'); return }
    if (selected.length === 0) { setError('Selecciona al menos un uso de correo.'); return }
    onSave({ email: address.trim() || null, contact: contact.trim() || null, usageCodes: selected })
  }
  function toggle(code: number) { setSelected(current => current.includes(code) ? current.filter(item => item !== code) : [...current, code]) }
  const unavailable = Boolean(catalogError) || usages.length === 0
  return <div className="side-panel-backdrop"><aside className="side-panel" role="dialog" aria-modal="true" aria-labelledby="email-panel-title"><h4 id="email-panel-title">{email ? 'Editar correo' : 'Agregar correo'}</h4>{catalogError && <div className="notice notice-error" role="alert">{catalogError}</div>}{!catalogError && usages.length === 0 && <p role="status">No hay usos de correo disponibles.</p>}<form onSubmit={submit}><label>Correo electrónico<input type="email" autoComplete="off" value={address} onChange={event => setAddress(event.target.value)} placeholder={email ? 'Dejar vacío para conservar' : undefined} required={!email} /></label><label>Contacto<input value={contact} maxLength={250} onChange={event => setContact(event.target.value)} /></label><fieldset><legend>Usos del correo</legend>{usages.map(usage => <label key={usage.code}><input type="checkbox" checked={selected.includes(usage.code)} onChange={() => toggle(usage.code)} />{usage.description}</label>)}</fieldset>{error && <div className="notice notice-error" role="alert">{error}</div>}<div className="side-panel-actions"><button type="button" onClick={onCancel}>Cancelar</button><button type="submit" disabled={busy || unavailable}>{busy ? 'Guardando…' : 'Guardar correo'}</button></div></form></aside></div>
}

function AccountManagement({ accounts, personId, canWrite, onUnauthorized, onRefresh }: { accounts: ManagedCustomerAccount[]; personId: number; canWrite: boolean; onUnauthorized: () => void; onRefresh: () => Promise<void> }) {
  const [editing, setEditing] = useState<ManagedCustomerAccount | null>(null)
  const [open, setOpen] = useState(false)
  const [busy, setBusy] = useState(false)
  const [message, setMessage] = useState('')
  const [bankCatalog, setBankCatalog] = useState<CustomerBank[]>([])
  const [bankCatalogLoading, setBankCatalogLoading] = useState(false)
  const [bankCatalogError, setBankCatalogError] = useState('')
  async function save(payload: CustomerAccountPayload) {
    setBusy(true); setMessage('')
    try { if (editing) await updateCustomerAccount(personId, editing.accountId, { ...payload, expectedModifiedAt: editing.modifiedAt }); else await createCustomerAccount(personId, payload); setOpen(false); setEditing(null); await onRefresh(); setMessage('Cuenta guardada correctamente.') }
    catch (error) { if (error instanceof ApiError && error.status === 401) onUnauthorized(); else if (error instanceof ApiError && error.status === 409 && error.code === 'account_duplicate') setMessage('Ya existe una cuenta o CLABE activa para este cliente.'); else if (error instanceof ApiError && error.status === 409 && error.code === 'account_modified') setMessage('La cuenta cambió; vuelve a cargarla antes de guardar.'); else setMessage(error instanceof ApiError ? apiErrorMessage(error, 'No fue posible guardar la cuenta.') : 'No fue posible guardar la cuenta.') }
    finally { setBusy(false) }
  }
  async function openPanel(account: ManagedCustomerAccount | null) {
    setEditing(account); setOpen(true); setBankCatalog([]); setBankCatalogError(''); setBankCatalogLoading(true)
    try { setBankCatalog(await getCustomerBanks()) }
    catch (error) { if (error instanceof ApiError && error.status === 401) onUnauthorized(); else setBankCatalogError('No fue posible cargar el catálogo de bancos.') }
    finally { setBankCatalogLoading(false) }
  }
  async function changeState(account: ManagedCustomerAccount, activate: boolean) {
    if (!activate && !window.confirm('¿Deseas desactivar esta cuenta? El registro se conservará.')) return
    setBusy(true); setMessage('')
    try { const payload = { expectedModifiedAt: account.modifiedAt }; if (activate) await activateCustomerAccount(personId, account.accountId, payload); else await deactivateCustomerAccount(personId, account.accountId, payload); await onRefresh(); setMessage(activate ? 'Cuenta activada.' : 'Cuenta desactivada.') }
    catch (error) { if (error instanceof ApiError && error.status === 401) onUnauthorized(); else if (error instanceof ApiError && error.status === 409 && error.code === 'account_duplicate') setMessage('No se puede activar: existe un duplicado activo.'); else setMessage(error instanceof ApiError ? apiErrorMessage(error, 'No fue posible actualizar la cuenta.') : 'No fue posible actualizar la cuenta.') }
    finally { setBusy(false) }
  }
  return <section className="customer-accounts" aria-labelledby="accounts-title"><div className="section-heading"><div><span className="eyebrow">Expediente</span><h4 id="accounts-title">Cuentas</h4></div>{canWrite && <button type="button" onClick={() => void openPanel(null)}>Agregar</button>}</div>{message && <div className="notice notice-status" role="status">{message}</div>}{accounts.length === 0 ? <p>No hay cuentas registradas.</p> : <div className="address-list">{accounts.map(account => <article className="address-card" key={account.accountId}><div><strong>{account.bankName ?? `Banco ${account.bankId}`}</strong><p>{account.currencyName ?? `Moneda ${account.currencyCode}`} · {account.accountTypeName ?? `Tipo ${account.accountTypeCode}`} · Sucursal {account.branchNumber}</p><small>{account.maskedAccountNumber ?? 'Cuenta no disponible'} · {account.maskedClabe ?? 'CLABE no disponible'} · {account.status === 1 ? 'Activo' : 'Inactivo'}</small></div>{canWrite && <div className="address-actions"><button type="button" disabled={busy} onClick={() => void openPanel(account)}>Editar</button>{account.status === 1 ? <button type="button" disabled={busy} onClick={() => void changeState(account, false)}>Desactivar</button> : <button type="button" disabled={busy} onClick={() => void changeState(account, true)}>Activar</button>}</div>}</article>)}</div>}{open && <AccountPanel account={editing} banks={bankCatalog} banksLoading={bankCatalogLoading} bankError={bankCatalogError} busy={busy} onCancel={() => { setOpen(false); setEditing(null) }} onSave={(payload) => void save(payload)} />}</section>
}

function AccountPanel({ account, banks, banksLoading, bankError, busy, onCancel, onSave }: { account: ManagedCustomerAccount | null; banks: CustomerBank[]; banksLoading: boolean; bankError: string; busy: boolean; onCancel: () => void; onSave: (payload: CustomerAccountPayload) => void }) {
  const [form, setForm] = useState({ bankId: String(account?.bankId ?? ''), branchNumber: String(account?.branchNumber ?? ''), currencyCode: String(account?.currencyCode ?? 1), accountTypeCode: String(account?.accountTypeCode ?? 1), accountNumber: '', clabe: '' })
  const [error, setError] = useState('')
  const historicalBank = account && !banks.some(bank => bank.bankId === account.bankId) ? { bankId: account.bankId, bankName: `${account.bankName ?? `Banco ${account.bankId}`} (no disponible)` } : null
  const bankOptions = historicalBank ? [historicalBank, ...banks] : banks
  function submit(event: FormEvent<HTMLFormElement>) { event.preventDefault(); if (!form.bankId || !form.branchNumber || (!account && (!form.accountNumber.trim() || !form.clabe.trim()))) { setError('Completa los campos obligatorios de la cuenta.'); return } onSave({ bankId: Number(form.bankId), branchNumber: Number(form.branchNumber), currencyCode: Number(form.currencyCode), accountNumber: form.accountNumber || null, clabe: form.clabe || null, accountTypeCode: Number(form.accountTypeCode) }) }
  const cannotSave = busy || banksLoading || Boolean(bankError) || bankOptions.length === 0
  return <div className="side-panel-backdrop"><aside className="side-panel" role="dialog" aria-modal="true" aria-labelledby="account-panel-title"><h4 id="account-panel-title">{account ? 'Editar cuenta' : 'Agregar cuenta'}</h4>{banksLoading && <p role="status">Cargando bancos…</p>}{bankError && <div className="notice notice-error" role="alert">{bankError}</div>}{!banksLoading && !bankError && bankOptions.length === 0 && <div className="notice notice-error" role="alert">No existen bancos disponibles para registrar la cuenta.</div>}<form onSubmit={submit}><label>Banco<select value={form.bankId} onChange={event => setForm({ ...form, bankId: event.target.value })} required disabled={banksLoading || Boolean(bankError) || bankOptions.length === 0}><option value="">Selecciona un banco</option>{bankOptions.map(bank => <option key={bank.bankId} value={bank.bankId} disabled={bank.bankId === historicalBank?.bankId}>{bank.bankName}</option>)}</select></label><label>Sucursal<input inputMode="numeric" value={form.branchNumber} onChange={event => setForm({ ...form, branchNumber: event.target.value })} required /></label><label>Moneda<select value={form.currencyCode} onChange={event => setForm({ ...form, currencyCode: event.target.value })}><option value="1">Pesos</option><option value="2">Dólares</option><option value="3">UDIS</option></select></label><label>Tipo de cuenta<select value={form.accountTypeCode} onChange={event => setForm({ ...form, accountTypeCode: event.target.value })}><option value="1">Cheques</option><option value="2">Tarjeta de crédito</option><option value="3">Tarjeta de débito</option></select></label><label>Número de cuenta<input inputMode="numeric" autoComplete="off" value={form.accountNumber} onChange={event => setForm({ ...form, accountNumber: event.target.value })} placeholder={account ? 'Dejar vacío para conservar' : undefined} required={!account} /></label><label>CLABE<input inputMode="numeric" autoComplete="off" value={form.clabe} onChange={event => setForm({ ...form, clabe: event.target.value })} placeholder={account ? 'Dejar vacío para conservar' : undefined} required={!account} /></label>{error && <div className="notice notice-error" role="alert">{error}</div>}<div className="side-panel-actions"><button type="button" onClick={onCancel}>Cancelar</button><button type="submit" disabled={cannotSave}>{busy ? 'Guardando…' : 'Guardar cuenta'}</button></div></form></aside></div>
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

function PhoneManagement({ phones, personId, canWrite, onUnauthorized, onRefresh }: { phones: ManagedCustomerPhone[]; personId: number; canWrite: boolean; onUnauthorized: () => void; onRefresh: () => Promise<void> }) {
  const [editing, setEditing] = useState<ManagedCustomerPhone | null>(null)
  const [panelOpen, setPanelOpen] = useState(false)
  const [pendingDeactivate, setPendingDeactivate] = useState<ManagedCustomerPhone | null>(null)
  const [busy, setBusy] = useState(false)
  const [message, setMessage] = useState('')
  async function save(payload: CustomerPhonePayload) {
    setBusy(true); setMessage('')
    try {
      if (editing) await updateCustomerPhone(personId, editing.phoneId, { ...payload, expectedModifiedAt: editing.modifiedAt })
      else await createCustomerPhone(personId, payload)
      setPanelOpen(false); setEditing(null); await onRefresh(); setMessage('Teléfono guardado correctamente.')
    } catch (error) { handlePhoneError(error, onUnauthorized, setMessage, 'No fue posible guardar el teléfono.') } finally { setBusy(false) }
  }
  async function changeState(phone: ManagedCustomerPhone, activate: boolean, replacementPhoneId?: number) {
    setBusy(true); setMessage('')
    try {
      const payload = { expectedModifiedAt: phone.modifiedAt, ...(replacementPhoneId ? { replacementPhoneId } : {}) }
      if (activate) await activateCustomerPhone(personId, phone.phoneId, payload)
      else await deactivateCustomerPhone(personId, phone.phoneId, payload)
      setPendingDeactivate(null); await onRefresh(); setMessage(activate ? 'Teléfono activado.' : 'Teléfono desactivado.')
    } catch (error) { handlePhoneError(error, onUnauthorized, setMessage, 'No fue posible actualizar el teléfono.') } finally { setBusy(false) }
  }
  return <section className="customer-phones" aria-labelledby="phones-title"><div className="section-heading"><div><span className="eyebrow">Expediente</span><h4 id="phones-title">Teléfonos</h4></div>{canWrite && <button type="button" onClick={() => { setEditing(null); setPanelOpen(true) }}>Agregar</button>}</div>{message && <div className="notice notice-status" role="status">{message}</div>}{phones.length === 0 ? <p>No hay teléfonos registrados.</p> : <div className="phone-list">{phones.map(phone => <article className="phone-card" key={phone.phoneId}><div><strong>{phoneTypeName(phone.phoneTypeCode)}</strong><p>{[phone.areaCode, phone.phoneNumber, phone.extension].filter(Boolean).join(' ') || 'Teléfono sin número'}</p><small>{phone.status === 'InheritedInactive' ? 'Inactivo heredado' : phone.status === 'Active' ? 'Activo' : 'Inactivo'} · {phone.isDefault ? 'Predeterminado' : 'No predeterminado'}</small></div>{canWrite && <div className="phone-actions">{phone.status === 'InheritedInactive' ? <button type="button" disabled={busy} onClick={() => void changeState(phone, true)}>Activar</button> : phone.status === 'Active' ? <><button type="button" disabled={busy} onClick={() => { setEditing(phone); setPanelOpen(true) }}>Editar</button><button type="button" disabled={busy} onClick={() => setPendingDeactivate(phone)}>Desactivar</button></> : <><button type="button" disabled={busy} onClick={() => { setEditing(phone); setPanelOpen(true) }}>Editar</button><button type="button" disabled={busy} onClick={() => void changeState(phone, true)}>Activar</button></>}</div>}</article>)}</div>}{pendingDeactivate && <PhoneDeactivatePanel phone={pendingDeactivate} phones={phones} busy={busy} onCancel={() => setPendingDeactivate(null)} onConfirm={(replacement) => void changeState(pendingDeactivate, false, replacement)} />}{panelOpen && <PhonePanel phone={editing} busy={busy} onCancel={() => { setPanelOpen(false); setEditing(null) }} onSave={(payload) => void save(payload)} />}</section>
}

function PhonePanel({ phone, busy, onCancel, onSave }: { phone: ManagedCustomerPhone | null; busy: boolean; onCancel: () => void; onSave: (payload: CustomerPhonePayload) => void }) {
  const [form, setForm] = useState({ phoneTypeCode: String(phone?.phoneTypeCode ?? 1), longDistanceCode: phone?.longDistanceCode ?? '', areaCode: phone?.areaCode ?? '', phoneNumber: phone?.phoneNumber ?? '', extension: phone?.extension ?? '', contactName: phone?.contactName ?? '' })
  const [isDefault, setIsDefault] = useState(phone?.isDefault ?? false)
  const [error, setError] = useState('')
  function submit(event: FormEvent<HTMLFormElement>) { event.preventDefault(); if (!form.phoneNumber.trim()) { setError('Completa el número telefónico.'); return } onSave({ phoneTypeCode: Number(form.phoneTypeCode), longDistanceCode: form.longDistanceCode, areaCode: form.areaCode, phoneNumber: form.phoneNumber, extension: form.extension, contactName: form.contactName, isDefault }) }
  return <div className="side-panel-backdrop"><aside className="side-panel" role="dialog" aria-modal="true" aria-labelledby="phone-panel-title"><h4 id="phone-panel-title">{phone ? 'Editar teléfono' : 'Agregar teléfono'}</h4><form onSubmit={submit}><label>Tipo de teléfono<select value={form.phoneTypeCode} onChange={event => setForm({ ...form, phoneTypeCode: event.target.value })}>{[[1, 'Casa'], [2, 'Oficina'], [3, 'Celular'], [4, 'Fax'], [5, 'Fiscal'], [6, 'Actividad económica'], [10, 'Otros'], [11, 'Otros casa'], [12, 'Otros oficina'], [13, 'Otros celular']].map(([value, label]) => <option key={value} value={value}>{label}</option>)}</select></label><PhoneInput label="Lada" value={form.areaCode} onChange={value => setForm({ ...form, areaCode: value })} /><PhoneInput label="Número" value={form.phoneNumber} onChange={value => setForm({ ...form, phoneNumber: value })} required /><PhoneInput label="Extensión" value={form.extension} onChange={value => setForm({ ...form, extension: value })} /><PhoneInput label="Contacto" value={form.contactName} onChange={value => setForm({ ...form, contactName: value })} /><label><input type="checkbox" checked={isDefault} onChange={event => setIsDefault(event.target.checked)} />Predeterminado</label>{error && <div className="notice notice-error" role="alert">{error}</div>}<div className="side-panel-actions"><button type="button" onClick={onCancel}>Cancelar</button><button type="submit" disabled={busy}>{busy ? 'Guardando…' : 'Guardar teléfono'}</button></div></form></aside></div>
}

function PhoneDeactivatePanel({ phone, phones, busy, onCancel, onConfirm }: { phone: ManagedCustomerPhone; phones: ManagedCustomerPhone[]; busy: boolean; onCancel: () => void; onConfirm: (replacement?: number) => void }) {
  const replacements = phones.filter(item => item.phoneId !== phone.phoneId && item.status === 'Active')
  const [replacement, setReplacement] = useState('')
  return <div className="side-panel-backdrop"><aside className="side-panel" role="dialog" aria-modal="true" aria-labelledby="deactivate-phone-title"><h4 id="deactivate-phone-title">Desactivar teléfono</h4><p>Esta acción conserva el registro y lo marca como inactivo.</p>{phone.isDefault && <label>Teléfono activo de reemplazo<select value={replacement} onChange={event => setReplacement(event.target.value)} required><option value="">Selecciona un reemplazo</option>{replacements.map(item => <option key={item.phoneId} value={item.phoneId}>{phoneTypeName(item.phoneTypeCode)} · {item.phoneId}</option>)}</select></label>}<div className="side-panel-actions"><button type="button" onClick={onCancel}>Cancelar</button><button type="button" disabled={busy || phone.isDefault && !replacement} onClick={() => onConfirm(replacement ? Number(replacement) : undefined)}>Confirmar desactivación</button></div></aside></div>
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
function PhoneInput({ label, value, onChange, required = false }: { label: string; value: string; onChange: (value: string) => void; required?: boolean }) { return <label>{label}<input value={value} required={required} onChange={event => onChange(event.target.value)} /></label> }
function defaultUses(type: number): string[] { return type === 1 ? ['billing', 'statements', 'other'] : type === 2 ? ['billing'] : [] }
function friendlyUses(uses: string[]): string { return uses.map(use => use === 'billing' ? 'Facturación' : use === 'statements' ? 'Estado de cuenta' : 'Otros').join(', ') || 'Ninguno' }
function handleAddressError(error: unknown, onUnauthorized: () => void, setMessage: (message: string) => void, fallback: string) { if (error instanceof ApiError && error.status === 401) { onUnauthorized(); return } if (error instanceof ApiError && error.status === 409) { setMessage(error.code === 'address_default_required' ? 'Selecciona un domicilio activo de reemplazo.' : 'El domicilio cambió; vuelve a cargarlo antes de guardar.') } else setMessage(error instanceof ApiError ? apiErrorMessage(error, fallback) : fallback) }
function handlePhoneError(error: unknown, onUnauthorized: () => void, setMessage: (message: string) => void, fallback: string) { if (error instanceof ApiError && error.status === 401) { onUnauthorized(); return } if (error instanceof ApiError && error.status === 409) { setMessage(error.code === 'phone_default_required' ? 'Selecciona un teléfono activo de reemplazo.' : 'El teléfono cambió; vuelve a cargarlo antes de guardar.') } else setMessage(error instanceof ApiError ? apiErrorMessage(error, fallback) : fallback) }
function handleEmailError(error: unknown, onUnauthorized: () => void, setMessage: (message: string) => void, fallback: string) { if (error instanceof ApiError && error.status === 401) { onUnauthorized(); return } if (error instanceof ApiError && error.status === 409) { setMessage(error.code === 'email_duplicate' ? 'Ya existe ese correo activo para este cliente.' : 'El correo cambió; vuelve a cargarlo antes de guardar.') } else setMessage(error instanceof ApiError ? apiErrorMessage(error, fallback) : fallback) }
function friendlyEmailUses(codes: number[], catalog: CustomerEmailUsage[]): string { return codes.map(code => catalog.find(item => item.code === code)?.description ?? `Uso ${code}`).join(', ') || 'Ninguno' }
function phoneTypeName(code: number): string { return ({ 1: 'Casa', 2: 'Oficina', 3: 'Celular', 4: 'Fax', 5: 'Fiscal', 6: 'Actividad económica', 10: 'Otros', 11: 'Otros casa', 12: 'Otros oficina', 13: 'Otros celular' } as Record<number, string>)[code] ?? `Tipo ${code}` }
function Detail({ label, value }: { label: string; value: string | null | undefined }) { return <div className="detail-field"><dt>{label}</dt><dd>{value || 'No disponible'}</dd></div> }
