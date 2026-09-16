import { useState, type FormEvent } from 'react'
import { apiErrorMessage, ApiError } from '../../shared/api/apiClient'
import { getContractByNumber, searchContracts, type SafeContract } from '../auth/authApi'

type ContractsViewProps = {
  onUnauthorized: () => void
}

export function ContractsView({ onUnauthorized }: ContractsViewProps) {
  const [contractNumber, setContractNumber] = useState('')
  const [detail, setDetail] = useState<SafeContract | null>(null)
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
        <p>La respuesta muestra únicamente el identificador y el estado operativo.</p>
      </div>

      <form onSubmit={submit} className="search-form">
        <label className="search-value">
          Número de contrato
          <input
            value={contractNumber}
            onChange={(event) => setContractNumber(event.target.value)}
            placeholder="Captura el número de contrato"
            maxLength={64}
            autoComplete="off"
            required
          />
        </label>
        <button type="submit" disabled={busy}>{busy ? 'Consultando…' : 'Buscar contrato'}</button>
      </form>

      {message && <div className="notice notice-error" role="alert">{message}</div>}
      {detail && (
        <div className="notice" role="status">
          <strong>Contrato {detail.contractNumber}</strong>
          <span>{detail.statusName ?? 'Estado no disponible'}</span>
          {detail.operationTypeName && <span>{detail.operationTypeName}</span>}
          {searchCount !== null && <span>Coincidencias exactas: {searchCount}</span>}
        </div>
      )}
    </section>
  )
}