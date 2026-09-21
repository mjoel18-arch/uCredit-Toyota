import { useState, type FormEvent } from 'react'
import { apiErrorMessage, ApiError } from '../../shared/api/apiClient'
import {
  getContractAmortization,
  getContractByNumber,
  searchContracts,
  type ContractAmortization,
  type ContractAmortizationPayment,
  type ContractDetail,
} from '../auth/authApi'
import { formatAmount, formatCurrency, formatDate, formatShortDate, formatText } from './contractFormatting'

type ContractsViewProps = {
  productName: string
  customerName: string
  onUnauthorized: () => void
}

export function ContractsView({ onUnauthorized }: ContractsViewProps) {
  const [contractNumber, setContractNumber] = useState('')
  const [detail, setDetail] = useState<ContractDetail | null>(null)
  const [amortization, setAmortization] = useState<ContractAmortization | null>(null)
  const [amortizationNotFound, setAmortizationNotFound] = useState(false)
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
    setAmortization(null)
    setAmortizationNotFound(false)
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

      let schedule: ContractAmortization | null = null
      let scheduleNotFound = false
      try {
        schedule = await getContractAmortization(requestedNumber)
      } catch (scheduleError) {
        if (scheduleError instanceof ApiError && scheduleError.status === 404) {
          scheduleNotFound = true
        } else {
          throw scheduleError
        }
      }

      setDetail(contract)
      setAmortization(schedule)
      setAmortizationNotFound(scheduleNotFound)
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
        <h2 id="contracts-title">Consulta de contratos</h2>
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
      {amortization && detail && <AmortizationScheduleCard detail={detail} schedule={amortization} />}
      {detail && amortizationNotFound && (
        <div className="notice" role="status">No hay una tabla de amortización tipo 1 disponible para este contrato.</div>
      )}
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

function AmortizationScheduleCard({
  detail,
  schedule,
}: {
  detail: ContractDetail
  schedule: ContractAmortization
}) {
  return (
    <article className="amortization-card" aria-labelledby="amortization-title">
      <header className="contract-detail-header">
        <div>
          <span className="eyebrow">Consulta de sólo lectura</span>
          <h3 id="amortization-title">Tabla de amortización</h3>
        </div>
        <span className="detail-match">Versión vigente: {schedule.version}</span>
      </header>

      {schedule.downPayment && (
        <section className="amortization-down-payment" aria-labelledby="down-payment-title">
          <h4 id="down-payment-title">Enganche</h4>
          <dl className="detail-grid">
            <DetailField label="Pago" value={schedule.downPayment.paymentNumber} />
            <DetailField label="Monto total" value={formatAmount(schedule.downPayment.totalPayment, detail.currencyCode)} />
            <DetailField label="Pago con IVA" value={formatAmount(schedule.downPayment.paymentWithIva, detail.currencyCode)} />
            <DetailField label="Estado" value={translatePaymentStatus(schedule.downPayment.status)} />
          </dl>
        </section>
      )}

      <div className="amortization-table-wrapper">
        <table className="amortization-table">
          <caption>Rentas ordinarias del financiamiento</caption>
          <thead>
            <tr>
              <th scope="col">Pago</th>
              <th scope="col">Periodo</th>
              <th scope="col">Fecha de exigibilidad</th>
              <th scope="col">Saldo inicial/base</th>
              <th scope="col">Saldo insoluto</th>
              <th scope="col">Amortización</th>
              <th scope="col">Interés</th>
              <th scope="col">IVA</th>
              <th scope="col">Pago sin IVA</th>
              <th scope="col">Pago con IVA/total</th>
              <th scope="col">Estado</th>
            </tr>
          </thead>
          <tbody>
            {schedule.payments.map((payment) => (
              <AmortizationRow key={payment.paymentNumber} detail={detail} payment={payment} />
            ))}
          </tbody>
        </table>
      </div>
    </article>
  )
}

function AmortizationRow({ detail, payment }: { detail: ContractDetail; payment: ContractAmortizationPayment }) {
  return (
    <tr>
      <th scope="row">{payment.paymentNumber}</th>
      <td>{formatShortDate(payment.startDate)} – {formatShortDate(payment.endDate)}</td>
      <td>{formatShortDate(payment.dueDate)}</td>
      <td>{formatAmount(payment.calculationBase, detail.currencyCode)}</td>
      <td>{formatAmount(payment.outstandingBalance, detail.currencyCode)}</td>
      <td>{formatAmount(payment.amortization, detail.currencyCode)}</td>
      <td>{formatAmount(payment.interest, detail.currencyCode)}</td>
      <td>{formatAmount(payment.iva, detail.currencyCode)}</td>
      <td>{formatAmount(payment.payment, detail.currencyCode)}</td>
      <td>{formatAmount(payment.totalPayment, detail.currencyCode)}</td>
      <td>{translatePaymentStatus(payment.status)}</td>
    </tr>
  )
}

function translatePaymentStatus(status: ContractAmortizationPayment['status']): string {
  return status === 'Generated' ? 'Generada' : 'Pendiente'
}

function DetailField({ label, value }: { label: string; value: string | number | null | undefined }) {
  return (
    <div className="detail-field">
      <dt>{label}</dt>
      <dd>{formatText(value)}</dd>
    </div>
  )
}
