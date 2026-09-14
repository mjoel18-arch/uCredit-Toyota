import { useMemo, useState, type FormEvent } from 'react'
import { useBranding } from './shared/branding/BrandingProvider'

type SearchField = 'contractNumber' | 'personId' | 'rfc' | 'personName' | 'vin'

const searchOptions: Array<{ value: SearchField; label: string }> = [
  { value: 'contractNumber', label: 'Número de contrato' },
  { value: 'personId', label: 'Clave de persona' },
  { value: 'rfc', label: 'RFC' },
  { value: 'personName', label: 'Nombre o razón social' },
  { value: 'vin', label: 'VIN' },
]

export function App() {
  const { theme, loading } = useBranding()
  const [field, setField] = useState<SearchField>('contractNumber')
  const [value, setValue] = useState('')
  const [message, setMessage] = useState('')

  const selectedLabel = useMemo(
    () => searchOptions.find((option) => option.value === field)?.label ?? '',
    [field],
  )

  function submit(event: FormEvent) {
    event.preventDefault()
    setMessage(value.trim() ? `Búsqueda preparada por ${selectedLabel}.` : 'Captura un criterio de búsqueda.')
  }

  return (
    <div className="app-shell">
      <aside className="sidebar">
        <div className="brand">
          {theme.logoUrl ? <img src={theme.logoUrl} alt={`Logotipo de ${theme.applicationName}`} /> : <span className="brand-mark">uC</span>}
          <div>
            <strong>{theme.applicationName}</strong>
            <small>{loading ? 'Cargando identidad…' : theme.tenantCode}</small>
          </div>
        </div>
        <nav aria-label="Navegación principal">
          <a className="active" href="#contracts">Contratos</a>
          <a href="#people">Personas</a>
          <a href="#collections">Cobranza</a>
          <a href="#reports">Reportes</a>
        </nav>
      </aside>

      <main>
        <header className="topbar">
          <div>
            <span className="eyebrow">Primer vertical</span>
            <h1>Consulta de contratos</h1>
          </div>
          <div className="user-chip" aria-label="Usuario actual">JM</div>
        </header>

        <section className="hero-card" id="contracts">
          <div>
            <span className="status-dot" /> Consulta de sólo lectura
            <h2>Encuentra un contrato</h2>
            <p>Busca de forma segura por uno de los criterios disponibles.</p>
          </div>

          <form onSubmit={submit} className="search-form">
            <label>
              Buscar por
              <select value={field} onChange={(event) => setField(event.target.value as SearchField)}>
                {searchOptions.map((option) => <option key={option.value} value={option.value}>{option.label}</option>)}
              </select>
            </label>
            <label className="search-value">
              {selectedLabel}
              <input
                value={value}
                onChange={(event) => setValue(event.target.value)}
                placeholder={`Captura ${selectedLabel.toLowerCase()}`}
              />
            </label>
            <button type="submit">Buscar contrato</button>
          </form>

          {message && <div className="notice" role="status">{message}</div>}
        </section>

        <section className="metrics" aria-label="Resumen del módulo">
          <article><span>Arquitectura</span><strong>Modular</strong><small>Separación por capacidades</small></article>
          <article><span>Acceso</span><strong>Lectura</strong><small>Sin cambios en Legacy</small></article>
          <article><span>Seguridad</span><strong>Parametrizada</strong><small>Sin SQL concatenado</small></article>
        </section>
      </main>
    </div>
  )
}

