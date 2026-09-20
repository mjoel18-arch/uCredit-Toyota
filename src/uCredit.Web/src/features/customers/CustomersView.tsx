import { useState, type FormEvent } from 'react'
import { ApiError, apiErrorMessage } from '../../shared/api/apiClient'
import { getCustomerByPersonId, searchCustomers, type CustomerDetail, type CustomerListItem } from '../auth/authApi'
import { CustomerCreateView } from './CustomerCreateView'

export function CustomersView({ productName, customerName, onUnauthorized, canCreate, onCreated }: { productName: string; customerName: string; onUnauthorized: () => void; canCreate: boolean; onCreated: (personId: number, pepValidationStatus?: string) => void }) {
  const [personId, setPersonId] = useState('')
  const [rfc, setRfc] = useState('')
  const [name, setName] = useState('')
  const [items, setItems] = useState<CustomerListItem[]>([])
  const [detail, setDetail] = useState<CustomerDetail | null>(null)
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
    setBusy(true); setMessage(''); setDetail(null)
    try {
      const result = await searchCustomers({ personId: personId ? Number(personId) : undefined, rfc: rfc.trim() || undefined, name: name.trim() || undefined, page: requestedPage })
      setItems(result.items); setPage(result.page); setTotal(result.total)
    } catch (error) {
      if (error instanceof ApiError && error.status === 401) { onUnauthorized(); return }
      setMessage(error instanceof ApiError && error.status === 404 ? 'No se encontró el cliente.' : apiErrorMessage(error, 'No fue posible consultar los clientes.'))
    } finally { setBusy(false) }
  }

  async function openDetail(item: CustomerListItem) {
    try { setDetail(await getCustomerByPersonId(item.personId)); setMessage('') }
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
    {detail && <CustomerDetailCard detail={detail} />}
    <div className="customer-upcoming"><button type="button" disabled>Editar cliente · Próximamente</button><button type="button" disabled>Crear propuesta · Próximamente</button><button type="button" disabled>Alta directa de contrato · Próximamente</button></div>
  </section>
}

function CustomerDetailCard({ detail }: { detail: CustomerDetail }) {
  return <article className="contract-detail" aria-labelledby="customer-detail-title"><header className="contract-detail-header"><div><span className="eyebrow">Cliente</span><h3 id="customer-detail-title">{detail.name}</h3></div><span className="detail-match">ID {detail.personId}</span></header><dl className="detail-grid"><Detail label="RFC" value={detail.rfc} /><Detail label="Personalidad" value={detail.legalPersonality.description} /><Detail label="Estatus" value={detail.status.description} /><Detail label="Teléfono principal" value={detail.primaryPhone ? [detail.primaryPhone.areaCode, detail.primaryPhone.phoneNumber].filter(Boolean).join(' ') : null} /><Detail label="Correo" value={detail.activeEmails[0]?.email} /><Detail label="Domicilio" value={detail.primaryAddress ? [detail.primaryAddress.streetAndNumber, detail.primaryAddress.exteriorNumber, detail.primaryAddress.city, detail.primaryAddress.state].filter(Boolean).join(', ') : null} /></dl><h4>Teléfonos activos</h4><ul>{detail.activePhones.map(phone => <li key={phone.phoneId}>{[phone.areaCode, phone.phoneNumber, phone.extension].filter(Boolean).join(' ')}</li>)}</ul><h4>Correos activos</h4><ul>{detail.activeEmails.map(email => <li key={email.emailId}>{email.email ?? 'No disponible'}</li>)}</ul></article>
}

function Detail({ label, value }: { label: string; value: string | null | undefined }) { return <div className="detail-field"><dt>{label}</dt><dd>{value || 'No disponible'}</dd></div> }
