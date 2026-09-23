import { useEffect, useRef, useState, type FormEvent } from 'react'
import { ApiError, apiErrorMessage } from '../../shared/api/apiClient'
import {
  getContractAddresses,
  getContractCfdiUses,
  getContractCnbv,
  getContractLateRate,
  getContractOperations,
  getContractOrdinaryRate,
  getCustomerByPersonId,
  getCustomerProfileReadiness,
  previewContract,
  type ContractAddressOption,
  type ContractCfdiUseCatalog,
  type ContractCnbvCatalog,
  type ContractLateRateConfiguration,
  type ContractOperationCatalog,
  type ContractPreviewResponse,
  type ContractRateConfiguration,
  type CustomerDetail,
  type CustomerProfileReadiness,
} from '../auth/authApi'

class ContractAddressCatalogError extends Error {
  constructor(readonly originalError: unknown) {
    super('Contract address catalog request failed')
    this.name = 'ContractAddressCatalogError'
  }
}

export function ContractCreateView({ onBack, onUnauthorized }: { onBack: () => void; onUnauthorized: () => void }) {
  const [personId, setPersonId] = useState('')
  const [customer, setCustomer] = useState<CustomerDetail | null>(null)
  const [readiness, setReadiness] = useState<CustomerProfileReadiness | null>(null)
  const [operations, setOperations] = useState<ContractOperationCatalog[]>([])
  const [cnbv, setCnbv] = useState<ContractCnbvCatalog[]>([])
  const [cfdiUses, setCfdiUses] = useState<ContractCfdiUseCatalog[]>([])
  const [addresses, setAddresses] = useState<ContractAddressOption[]>([])
  const [rate, setRate] = useState<ContractRateConfiguration | null>(null)
  const [lateRate, setLateRate] = useState<ContractLateRateConfiguration | null>(null)
  const [operationCode, setOperationCode] = useState('')
  const [selectedCnbv, setSelectedCnbv] = useState('')
  const [selectedCfdi, setSelectedCfdi] = useState('')
  const [selectedAddress, setSelectedAddress] = useState('')
  const [capital, setCapital] = useState('0')
  const [downPayment, setDownPayment] = useState('0')
  const [iva, setIva] = useState('0')
  const [startDate, setStartDate] = useState('')
  const [firstPaymentDate, setFirstPaymentDate] = useState('')
  const [disbursementDate, setDisbursementDate] = useState('')
  const [term, setTerm] = useState('')
  const [nominalRate, setNominalRate] = useState('')
  const [message, setMessage] = useState('')
  const [catalogMessage, setCatalogMessage] = useState('')
  const [busy, setBusy] = useState(false)
  const [loadingCatalogs, setLoadingCatalogs] = useState(false)
  const [preview, setPreview] = useState<ContractPreviewResponse | null>(null)
  const personInput = useRef<HTMLInputElement>(null)

  useEffect(() => {
    let cancelled = false
    void getContractOperations().then(items => { if (!cancelled) setOperations(items) }).catch(error => { if (!cancelled) setCatalogMessage(apiErrorMessage(error, 'No fue posible cargar las operaciones.')) })
    return () => { cancelled = true }
  }, [])

  async function loadCustomer(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const parsed = Number(personId)
    if (!Number.isInteger(parsed) || parsed <= 0) { setMessage('Captura un identificador de cliente válido.'); personInput.current?.focus(); return }
    setBusy(true); setMessage(''); setPreview(null); setOperationCode(''); setCnbv([]); setCfdiUses([]); setAddresses([])
    try {
      const addressRequest = getContractAddresses(parsed).catch(error => {
        throw new ContractAddressCatalogError(error)
      })
      const [loadedCustomer, loadedReadiness, loadedCfdi, loadedAddresses] = await Promise.all([
        getCustomerByPersonId(parsed),
        getCustomerProfileReadiness(parsed),
        getContractCfdiUses(parsed),
        addressRequest,
      ])
      setCustomer(loadedCustomer); setReadiness(loadedReadiness); setCfdiUses(loadedCfdi); setAddresses(loadedAddresses)
      setSelectedCfdi(loadedCfdi[0]?.code ?? ''); setSelectedAddress(String(loadedAddresses[0]?.id ?? ''))
    } catch (error) {
      const sourceError = error instanceof ContractAddressCatalogError ? error.originalError : error
      if (sourceError instanceof ApiError && sourceError.status === 401) {
        onUnauthorized()
      } else if (error instanceof ContractAddressCatalogError && sourceError instanceof ApiError && sourceError.status === 500) {
        setMessage('Ocurrió un error interno al consultar domicilios.')
      } else if (error instanceof ContractAddressCatalogError && sourceError instanceof ApiError && sourceError.status === 503) {
        setMessage('El servicio no está disponible en este momento.')
      } else {
        setMessage(apiErrorMessage(sourceError, error instanceof ContractAddressCatalogError ? 'No fue posible cargar los domicilios.' : 'No fue posible cargar el cliente.'))
      }
    } finally { setBusy(false) }
  }

  async function selectOperation(code: string) {
    setOperationCode(code); setCnbv([]); setSelectedCnbv(''); setRate(null); setLateRate(null); setCatalogMessage(''); setPreview(null)
    if (!code) return
    setLoadingCatalogs(true)
    try {
      const [loadedCnbv, loadedRate, loadedLateRate] = await Promise.all([getContractCnbv(code), getContractOrdinaryRate(code), getContractLateRate(code)])
      setCnbv(loadedCnbv); setSelectedCnbv(String(loadedCnbv.find(item => item.isDefault)?.id ?? loadedCnbv[0]?.id ?? '')); setRate(loadedRate); setLateRate(loadedLateRate)
    } catch (error) {
      setCatalogMessage(apiErrorMessage(error, 'La configuración del contrato no está disponible.'))
    } finally { setLoadingCatalogs(false) }
  }

  async function submitPreview(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); if (busy) return
    setBusy(true); setMessage(''); setPreview(null)
    try {
      const result = await previewContract({ personId: Number(personId), operationCode, capital: Number(capital), downPayment: Number(downPayment), iva: Number(iva), startDate, firstPaymentDate, disbursementRequestDate: disbursementDate, term: Number(term), nominalAnnualRate: Number(nominalRate), cnbvCode: Number(selectedCnbv), cfdiUseCode: selectedCfdi, addressId: Number(selectedAddress) })
      setPreview(result)
    } catch (error) {
      if (error instanceof ApiError && error.status === 401) onUnauthorized()
      else setMessage(apiErrorMessage(error, 'No fue posible validar la captura.'))
    } finally { setBusy(false) }
  }

  const readinessMissing = readiness?.canCreateContract === false
  return <section className="hero-card contract-create" aria-labelledby="contract-create-title">
    <div className="section-heading"><div><span className="eyebrow">Captura de contrato</span><h2 id="contract-create-title">Captura preliminar</h2></div><button type="button" className="button-secondary" onClick={onBack}>Volver a contratos</button></div>
    <p>Esta fase valida y calcula datos sin crear contratos ni escribir en Legacy.</p>
    {message && <div className="notice notice-error" role="alert">{message}</div>}
    {catalogMessage && <div className="notice notice-error" role="alert">{catalogMessage}</div>}
    <form className="search-form" onSubmit={(event) => void loadCustomer(event)}><label>Identificador del cliente<input ref={personInput} value={personId} onChange={event => setPersonId(event.target.value)} inputMode="numeric" required /></label><button type="submit" disabled={busy}>{busy ? 'Cargando…' : 'Cargar cliente'}</button></form>
    {customer && readiness && <form onSubmit={(event) => void submitPreview(event)} className="contract-foundation-form">
      <section className="contract-create-section"><h3>Cliente</h3><p>{customer.name} · ID {customer.personId}</p>{readinessMissing && <div className="notice notice-error" role="alert">El expediente está incompleto: {readiness.missingRequirements.join(', ')}. Completa Domicilios, Teléfonos y Cuentas antes de continuar.</div>}</section>
      <section className="contract-create-section"><h3>Tipo de operación</h3><label>Operación<select value={operationCode} onChange={event => void selectOperation(event.target.value)} required><option value="">Selecciona una operación</option>{operations.map(item => <option key={`${item.code}-${item.companyId}`} value={item.code} disabled={item.code !== 'CD'}>{item.description} {item.code !== 'CD' ? '(Próximamente)' : ''}</option>)}</select></label></section>
      <section className="contract-create-section"><h3>Propuesta</h3><p className="technical-note">Integración pendiente. No se consulta ni se envía KPR_FL_CVE.</p></section>
      <section className="contract-create-section"><h3>Generales</h3><div className="contract-form-grid"><label>Capital<input type="number" min="0" step="0.01" value={capital} onChange={event => setCapital(event.target.value)} required /></label><label>Enganche<input type="number" min="0" step="0.01" value={downPayment} onChange={event => setDownPayment(event.target.value)} required /></label><label>IVA<input type="number" min="0" step="0.01" value={iva} onChange={event => setIva(event.target.value)} required /></label><label>Fecha de inicio<input type="date" value={startDate} onChange={event => setStartDate(event.target.value)} required /></label><label>Primer pago<input type="date" value={firstPaymentDate} onChange={event => setFirstPaymentDate(event.target.value)} required /></label><label>Solicitud de desembolso<input type="date" value={disbursementDate} onChange={event => setDisbursementDate(event.target.value)} required /></label><label>Plazo<input type="number" min="1" value={term} onChange={event => setTerm(event.target.value)} required /></label><label>Tasa nominal anual<input type="number" min="0" step="0.0001" value={nominalRate} onChange={event => setNominalRate(event.target.value)} required /></label><label>CNBV<select value={selectedCnbv} onChange={event => setSelectedCnbv(event.target.value)} disabled={loadingCatalogs || cnbv.length === 0} required><option value="">Selecciona CNBV</option>{cnbv.map(item => <option key={item.id} value={item.id}>{item.description}</option>)}</select></label><label>Uso CFDI<select value={selectedCfdi} onChange={event => setSelectedCfdi(event.target.value)} disabled={!cfdiUses.length} required><option value="">Selecciona Uso CFDI</option>{cfdiUses.map(item => <option key={item.code} value={item.code}>{item.description}</option>)}</select></label><label>Domicilio<select value={selectedAddress} onChange={event => setSelectedAddress(event.target.value)} disabled={!addresses.length} required><option value="">Selecciona domicilio</option>{addresses.map(item => <option key={item.id} value={item.id}>{item.typeDescription ?? `Tipo ${item.typeCode}`} · {item.id}</option>)}</select></label></div></section>
      <section className="contract-create-section"><h3>Tasa</h3><p>{rate ? `${rate.description} · moneda ${rate.currencyCode}` : 'Selecciona una operación para resolver la tasa.'}</p></section>
      <section className="contract-create-section"><h3>Tasa moratoria</h3><p>{lateRate ? `Configuración disponible · puntos ${lateRate.points}` : 'Selecciona una operación para resolver la configuración moratoria.'}</p></section>
      <section className="contract-create-section"><h3>Pagos finales</h3><p className="technical-note">La configuración de pagos finales permanece pendiente; no se escribe información.</p></section>
      <button type="submit" disabled={busy || loadingCatalogs || !operationCode || readinessMissing}>{busy ? 'Validando…' : 'Validar captura'}</button>
      <button type="button" disabled>Creación pendiente de configuración</button>
    </form>}
    {preview && <section className="contract-preview" aria-live="polite"><h3>Resumen calculado</h3><p>{preview.canCreate ? 'La captura cumple las validaciones conocidas.' : 'La captura no puede crear un contrato todavía.'}</p><dl className="detail-grid"><div><dt>Monto a financiar</dt><dd>{preview.amounts.amountToFinance.toFixed(2)}</dd></div><div><dt>Fecha operativa</dt><dd>{preview.businessDate ?? 'No disponible'}</dd></div></dl>{preview.pendingRules.length > 0 && <ul>{preview.pendingRules.map(rule => <li key={rule.code}>{rule.message}</li>)}</ul>}</section>}
  </section>
}
