import { useState, type FormEvent } from 'react'
import { apiErrorMessage, ApiError } from '../../shared/api/apiClient'
import { getContractByNumber, searchContracts, type ContractDetail } from '../auth/authApi'
import { formatAmount, formatCurrency, formatDate, formatText } from './contractFormatting'

type ContractsViewProps = {
  onUnauthorized: () => void
}

export function ContractsView({ onUnauthorized }: ContractsViewProps) {
  const [contractNumber, setContractNumber] = useState('')
  const [detail, setDetail] = useState<ContractDetail | null>(null)
  const [searchCount, setSearchCount] = useState<number | null>(null)
  const [message, setMessage] = useState('')
  const [busy, setBusy] = useState(false)

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const requestedNumber = contractNumber.trim()
    if (!requestedNumber) {
      setMessage('Captura un número de contrato.')
      return
    }

    setBusy(true)
    setMessage('')
    setDetail(null)
    setSearchCount(null)

    try {
      const [contract, result] = await Promise.all([
        getContractByNumber(requestedNumber),
        searchContracts(requestedNumber),
      ])

      const matchingItems = result.items.filter((item) => item.contractNumber === requestedNumber)
      const hasUnexpectedItem = result.items.some((item) => item.contractNumber !== requestedNumber)
      const hasDuplicates = new Set(result.items.map((item) => item.contractNumber)).size !== result.items.length

      if (contract.contractNumber !== requestedNumber || hasUnexpectedItem || hasDuplicates || matchingItems.length === 0) {
        setMessage('La respuesta del servicio no coincide con la consulta solicitada.')
        return
      }

      setDetail(contract)
      setSearchCount(matchingItems.length)
    } catch (requestError) {
      if (requestError instanceof ApiError && requestError.status === 401) {
        onUnauthorized()
        return
      }

      if (requestError instanceof ApiError && requestError.status === 404) {
        setMessage('No se encontró el contrato dentro del alcance permitido.')
        return
      }

      setMessage(apiErrorMessage(requestError, 'No fue posible consultar el contrato.'))
    } finally {
      setBusy(false)
    }
  }

  return (
    <section className="hero-card" id="contracts" aria-labelledby="contracts-title">
      <div>
        <span className="status-dot" /> Consulta de sólo lectura
        <h2 id="contracts-title">Encuentra un contrato</h2>
        <p>Consulta la información operativa disponible para el alcance autorizado de tu tenant.</p>
      </div>

      <form onSubmit={submit} className="search-form">
        <label className="search-value">
          Número de contrato
          <input
            value={contractNumber}
            onChange={(event) => setContractNumber(event.target.value)}
            placeholder="Captura el número de contrato"
            maxLength={15}
            autoComplete="off"
            required
          />
        </label>
        <button type="submit" disabled={busy}>{busy ? 'Consultando…' : 'Buscar contrato'}</button>
      </form>

      {message && <div className="notice notice-error" role="alert">{message}</div>}
      {detail && <ContractDetailCard detail={detail} searchCount={searchCount} />}
    </section>
  )
}

function ContractDetailCard({ detail, searchCount }: { detail: ContractDetail; searchCount: number | null }) {
  const operationType = detail.operationType?.description ?? detail.operationType?.code

  return (
    <article className="contract-detail" aria-labelledby="contract-detail-title">
      <header className="contract-detail-header">
        <div>
          <span className="eyebrow">Resultado protegido</span>
          <h3 id="contract-detail-title">Contrato {detail.contractNumber}</h3>
        </div>
        {searchCount !== null && <span className="detail-match">Coincidencias exactas: {searchCount}</span>}
      </header>

      <section className="contract-detail-section" aria-labelledby="contract-identification-title">
        <h4 id="contract-identification-title">Identificación</h4>
        <dl className="detail-grid">
          <DetailField label="Número de contrato" value={detail.contractNumber} />
          <DetailField label="Estado" value={detail.status?.description} />
          <DetailField label="Tipo de operación" value={operationType} />
          <DetailField label="Persona o cliente" value={detail.customerName} />
        </dl>
      </section>

      <section className="contract-detail-section" aria-labelledby="contract-financial-title">
        <h4 id="contract-financial-title">Datos financieros</h4>
        <dl className="detail-grid">
          <DetailField label="Moneda" value={formatCurrency(detail.currencyCode, detail.currencyName)} />
          <DetailField label="Monto financiado" value={formatAmount(detail.financedAmount, detail.currencyCode)} />
          <DetailField label="Saldo insoluto" value={formatAmount(detail.outstandingBalance, detail.currencyCode)} />
          <DetailField label="Plazo actual" value={detail.currentTerm} />
          <DetailField label="Plazo original" value={detail.originalTerm} />
        </dl>
      </section>

      <section className="contract-detail-section" aria-labelledby="contract-dates-title">
        <h4 id="contract-dates-title">Fechas y plazo</h4>
        <dl className="detail-grid">
          <DetailField label="Fecha de inicio" value={formatDate(detail.startDate)} />
          <DetailField label="Fecha de activación" value={formatDate(detail.activationDate)} />
          <DetailField label="Fecha de desembolso" value={formatDate(detail.disbursementDate)} />
          <DetailField label="Fecha del primer pago" value={formatDate(detail.firstPaymentDate)} />
          <DetailField label="Fecha del último pago" value={formatDate(detail.lastPaymentDate)} />
        </dl>
      </section>
    </article>
  )
}

function DetailField({ label, value }: { label: string; value: string | number | null | undefined }) {
  return (
    <div className="detail-field">
      <dt>{label}</dt>
      <dd>{formatText(value)}</dd>
    </div>
  )
}
