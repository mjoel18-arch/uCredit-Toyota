import { useRef, useState, type FormEvent } from 'react'
import { ApiError, apiErrorMessage } from '../../shared/api/apiClient'
import { createCustomer, type CustomerCreatePayload } from '../auth/authApi'

export function getTemporaryTaxRegimeCode(legalPersonality: number): string {
  return legalPersonality === 2 ? '612' : legalPersonality === 20 ? '601' : '605'
}

const initial: CustomerCreatePayload = {
  legalPersonality: 1, rfc: '', firstName: '', paternalSurname: '', maternalSurname: '', constitutionOrBirthDate: '',
  countryCode: 1, groupCode: 1, riskCode: 1, contactFormCode: 1, taxRegimeCode: getTemporaryTaxRegimeCode(1),
  phoneTypeCode: 1, areaCode: '', phoneNumber: '', emailContact: '', email: '', emailUsageCodes: [1], pepConfirmed: false,
}

export function CustomerCreateView({ onBack, onCreated }: { onBack: () => void; onCreated: (personId: number, pepValidationStatus: 'Executed' | 'NotExecuted') => void }) {
  const [form, setForm] = useState<CustomerCreatePayload>(initial)
  const [busy, setBusy] = useState(false)
  const [message, setMessage] = useState('')
  const [pepConfirmationRequired, setPepConfirmationRequired] = useState(false)
  const [pepRetryUsed, setPepRetryUsed] = useState(false)
  const emailUsageGroupRef = useRef<HTMLFieldSetElement>(null)
  const set = (key: keyof CustomerCreatePayload, value: unknown) => setForm(current => ({ ...current, [key]: value }))
  async function submit(event: FormEvent) {
    event.preventDefault(); if (busy) return
    if (pepConfirmationRequired && form.pepConfirmed) {
      if (pepRetryUsed) return
      setPepRetryUsed(true)
    }
    if (form.emailUsageCodes.length === 0) {
      setMessage('Selecciona al menos un uso del correo.')
      emailUsageGroupRef.current?.focus()
      return
    }
    setBusy(true); setMessage('')
    try { const result = await createCustomer(form); onCreated(result.personId, result.pepValidationStatus) }
    catch (error) {
      if (error instanceof ApiError && error.status === 401) onBack()
      else if (error instanceof ApiError && error.status === 422 && error.code === 'pep_confirmation_required') {
        setPepConfirmationRequired(true)
        setMessage('La validación PEP encontró una coincidencia. Confirma que deseas continuar.')
      }
      else setMessage(error instanceof ApiError && error.status === 503 ? 'El servicio no está disponible; no se guardó información.' : apiErrorMessage(error, 'No fue posible crear el cliente.'))
    } finally { setBusy(false) }
  }
  return <section className="hero-card customer-create" aria-labelledby="customer-create-title">
    <button type="button" className="button-secondary" onClick={onBack}>Volver a Clientes</button>
    <span className="eyebrow">Alta controlada</span><h2 id="customer-create-title">Crear cliente</h2>
    <p>La información se valida antes de una única transacción Legacy. No se almacenan formularios localmente.</p>
    {message && <div className="notice notice-error" role="alert">{message}</div>}
    <form className="customer-create-form" onSubmit={(event) => void submit(event)}>
      <label>Personalidad<select value={form.legalPersonality} onChange={e => { const legalPersonality = Number(e.target.value); setForm(current => ({ ...current, legalPersonality, taxRegimeCode: getTemporaryTaxRegimeCode(legalPersonality) })) }}><option value="1">Física</option><option value="2">Física con actividad empresarial</option><option value="20" disabled>Moral (Próximamente)</option></select></label>
      <label>RFC<input required maxLength={13} value={form.rfc} onChange={e => set('rfc', e.target.value)} /></label>
      {form.legalPersonality === 20 ? <><label>Razón social<input required value={form.legalName ?? ''} onChange={e => set('legalName', e.target.value)} /></label><label>Régimen de capital<input required value={form.capitalRegime ?? ''} onChange={e => set('capitalRegime', e.target.value)} /></label></> : <><label>Nombre<input required value={form.firstName ?? ''} onChange={e => set('firstName', e.target.value)} /></label><label>Apellido paterno<input value={form.paternalSurname ?? ''} onChange={e => set('paternalSurname', e.target.value)} /></label><label>Apellido materno<input value={form.maternalSurname ?? ''} onChange={e => set('maternalSurname', e.target.value)} /></label></>}
      <label>Fecha de nacimiento o constitución<input required type="date" value={form.constitutionOrBirthDate} onChange={e => set('constitutionOrBirthDate', e.target.value)} /></label>
      <label>Lada<input required value={form.areaCode} onChange={e => set('areaCode', e.target.value)} /></label><label>Teléfono<input required value={form.phoneNumber} onChange={e => set('phoneNumber', e.target.value)} /></label><label>Contacto del correo<input required value={form.emailContact} onChange={e => set('emailContact', e.target.value)} /></label><label>Correo electrónico<input required type="email" value={form.email} onChange={e => set('email', e.target.value)} /></label>
      <fieldset ref={emailUsageGroupRef} tabIndex={-1} aria-describedby="email-usage-error" aria-invalid={form.emailUsageCodes.length === 0}>
        <legend>Usos del correo</legend>
        {[{ code: 1, label: 'Envío de facturas' }, { code: 2, label: 'Envío de estado de cuenta' }, { code: 3, label: 'Salesforce' }].map(({ code, label }) => <label key={code}><input type="checkbox" checked={form.emailUsageCodes.includes(code)} onChange={e => {
          if (!e.target.checked && form.emailUsageCodes.length === 1) {
            setMessage('Selecciona al menos un uso del correo.')
            emailUsageGroupRef.current?.focus()
            return
          }
          set('emailUsageCodes', e.target.checked ? [...form.emailUsageCodes, code] : form.emailUsageCodes.filter(item => item !== code))
          setMessage('')
        }} /> {label}</label>)}
        <span id="email-usage-error" className="field-error" role="status" aria-live="polite">{message === 'Selecciona al menos un uso del correo.' ? message : ''}</span>
      </fieldset>
      {pepConfirmationRequired && <fieldset className="pep-confirmation" aria-describedby="pep-confirmation-help">
        <legend>Confirmación PEP requerida</legend>
        <label><input type="checkbox" checked={form.pepConfirmed} onChange={event => set('pepConfirmed', event.target.checked)} /> Confirmo que deseo continuar después de revisar la coincidencia PEP.</label>
        <span id="pep-confirmation-help">La confirmación sólo se enviará al proveedor después de marcar esta opción.</span>
      </fieldset>}
      <div className="form-actions"><button type="submit" disabled={busy}>{busy ? 'Validando…' : 'Revisar y crear cliente'}</button><button type="button" className="button-secondary" onClick={onBack} disabled={busy}>Cancelar</button></div>
    </form>
  </section>
}
