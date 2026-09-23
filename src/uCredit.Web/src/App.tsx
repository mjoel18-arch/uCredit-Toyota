import { useEffect, useState, type FormEvent } from 'react'
import { useBranding } from './shared/branding/BrandingProvider'
import { ContractsView } from './features/contracts/ContractsView'
import { ContractCreateView } from './features/contracts/ContractCreateView'
import { CustomersView } from './features/customers/CustomersView'
import { useAuthSession } from './features/auth/useAuthSession'
import type { AuthSession } from './features/auth/authApi'
import type { BrandTheme } from './shared/branding/brandTheme'

function LoadingView({ label = 'Cargando…' }: { label?: string }) {
  return <main className="centered-page" aria-live="polite"><div className="loading-panel">{label}</div></main>
}

function LoginView({
  theme,
  busy,
  error,
  onLogin,
}: {
  theme: BrandTheme
  busy: boolean
  error: string
  onLogin: (userName: string, password: string) => Promise<void>
}) {
  const [userName, setUserName] = useState('')
  const [password, setPassword] = useState('')

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const submittedPassword = password
    setPassword('')
    await onLogin(userName.trim(), submittedPassword)
  }

  return (
    <main className="centered-page">
      <section className="auth-card" aria-labelledby="login-title">
        <BrandIdentity theme={theme} />
        <h1 id="login-title">Iniciar sesión</h1>
        <p>Usa tus credenciales de la instalación para continuar.</p>
        <form onSubmit={submit} className="auth-form">
          <label>
            Correo electrónico
            <input
              type="email"
              value={userName}
              onChange={(event) => setUserName(event.target.value)}
              autoComplete="username"
              maxLength={256}
              required
            />
          </label>
          <label>
            Contraseña
            <input
              type="password"
              value={password}
              onChange={(event) => setPassword(event.target.value)}
              autoComplete="current-password"
              maxLength={128}
              required
            />
          </label>
          {error && <div className="notice notice-error" role="alert">{error}</div>}
          <button type="submit" disabled={busy}>
            {busy ? 'Validando…' : 'Entrar'}
          </button>
        </form>
      </section>
    </main>
  )
}

function TenantSelector({
  theme,
  session,
  busy,
  error,
  onSelect,
}: {
  theme: BrandTheme
  session: AuthSession
  busy: boolean
  error: string
  onSelect: (tenantCode: string) => Promise<void>
}) {
  const [selectedCode, setSelectedCode] = useState(session.memberships[0]?.tenantCode ?? '')
  const memberships = session.memberships

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (selectedCode) {
      await onSelect(selectedCode)
    }
  }

  return (
    <main className="centered-page">
      <section className="auth-card tenant-card" aria-labelledby="tenant-title">
        <BrandIdentity theme={theme} suffix="· sesión autenticada" />
        <h1 id="tenant-title">Selecciona una cartera</h1>
        <p>Elige una membresía activa disponible para tu usuario.</p>
        {memberships.length === 0 ? (
          <div className="notice notice-error" role="alert">No hay tenants disponibles para esta instalación.</div>
        ) : (
          <form onSubmit={submit} className="auth-form">
            <label>
              Tenant
              <select value={selectedCode} onChange={(event) => setSelectedCode(event.target.value)} disabled={busy}>
                {memberships.map((membership) => (
                  <option key={membership.tenantId} value={membership.tenantCode}>
                    {membership.tenantName} ({membership.tenantCode})
                  </option>
                ))}
              </select>
            </label>
            {import.meta.env.DEV && selectedCode && (
              <small className="technical-note">
                CompanyId autorizados (Development): {formatCompanyIds(memberships.find((item) => item.tenantCode === selectedCode)?.allowedCompanyIds)}
              </small>
            )}
            {error && <div className="notice notice-error" role="alert">{error}</div>}
            <button type="submit" disabled={busy || !selectedCode}>
              {busy ? 'Seleccionando…' : 'Continuar'}
            </button>
          </form>
        )}
      </section>
    </main>
  )
}

function formatCompanyIds(companyIds: number[] | undefined): string {
  return companyIds && companyIds.length > 0 ? companyIds.join(', ') : 'ninguno'
}

function BrandIdentity({ theme, suffix }: { theme: BrandTheme; suffix?: string }) {
  return (
    <div className="auth-brand">
      <BrandLogo theme={theme} />
      <div>
        <span className="eyebrow">{theme.productName}{suffix ? ` ${suffix}` : ''}</span>
        {theme.customerName && <small>{theme.customerName}</small>}
      </div>
    </div>
  )
}

export function BrandLogo({ theme }: { theme: BrandTheme }) {
  const [logoAvailable, setLogoAvailable] = useState(Boolean(theme.logoUrl))

  return theme.logoUrl && logoAvailable
    ? <img className="brand-logo" src={theme.logoUrl} alt={theme.customerName || theme.productName} onError={() => setLogoAvailable(false)} />
    : <span className="brand-mark" aria-label={theme.productName}>{theme.productName}</span>
}

function initials(userName: string): string {
  return userName
    .split(/[\s@._-]+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase() ?? '')
    .join('') || 'UC'
}

function ApplicationView({
  theme,
  session,
  brandingLoading,
  onLogout,
  onUnauthorized,
}: {
  theme: BrandTheme
  session: AuthSession
  brandingLoading: boolean
  onLogout: () => Promise<void>
  onUnauthorized: () => void
}) {
  const hasContractsPermission = session.permissions.includes('contracts.read')
  const hasCustomersPermission = session.permissions.includes('customers.read')
  const hasCustomersWritePermission = session.permissions.includes('customers.write')
  const [activeModule, setActiveModule] = useState<'contracts' | 'customers'>('contracts')
  const [contractCreateOpen, setContractCreateOpen] = useState(false)

  return (
    <div className="app-shell">
      <aside className="sidebar">
        <div className="brand brand-stack">
          <strong className="brand-product">{theme.productName}</strong>
          <BrandLogo theme={theme} />
          <small className="brand-customer">{theme.customerName || (brandingLoading ? 'Cargando identidad…' : session.tenant?.tenantCode)}</small>
        </div>
        <nav aria-label="Navegación principal">
          <button type="button" className={activeModule === 'contracts' && !contractCreateOpen ? 'active' : ''} aria-current={activeModule === 'contracts' && !contractCreateOpen ? 'page' : undefined} onClick={() => { setActiveModule('contracts'); setContractCreateOpen(false) }}>Contratos</button>
          <button type="button" className={activeModule === 'customers' ? 'active' : ''} aria-current={activeModule === 'customers' ? 'page' : undefined} disabled={!hasCustomersPermission} aria-disabled={!hasCustomersPermission} onClick={() => { setActiveModule('customers'); setContractCreateOpen(false) }}>Clientes</button>
          <button type="button" className={activeModule === 'contracts' && contractCreateOpen ? 'active' : ''} aria-current={activeModule === 'contracts' && contractCreateOpen ? 'page' : undefined} disabled={!hasContractsPermission} aria-disabled={!hasContractsPermission} onClick={() => { setActiveModule('contracts'); setContractCreateOpen(true) }}>Captura de contrato</button>
          <button type="button" disabled aria-disabled="true">Cobranza · Próximamente</button>
          <button type="button" disabled aria-disabled="true">Reportes · Próximamente</button>
        </nav>
      </aside>

      <main>
        <header className="topbar">
          <div>
            <span className="eyebrow">Primer vertical</span>
            <h1>{theme.productName}</h1>
          </div>
          <div className="session-actions">
            <span className="user-chip" aria-label={'Usuario actual: ' + session.userName}>{initials(session.userName)}</span>
            <span className="session-user">{session.userName}</span>
            <button type="button" className="button-secondary" onClick={() => void onLogout()}>Cerrar sesión</button>
          </div>
        </header>

        {activeModule === 'customers' && hasCustomersPermission ? (
          <CustomersView productName={theme.productName} customerName={theme.customerName} onUnauthorized={onUnauthorized} canCreate={hasCustomersWritePermission} onCreated={() => undefined} />
        ) : hasContractsPermission && contractCreateOpen ? (
          <ContractCreateView onBack={() => { setContractCreateOpen(false); setActiveModule('contracts') }} onUnauthorized={onUnauthorized} />
        ) : hasContractsPermission ? (
          <ContractsView
            productName={theme.productName}
            customerName={theme.customerName}
            onUnauthorized={onUnauthorized}
            onCreateContract={() => { setActiveModule('contracts'); setContractCreateOpen(true) }}
          />
        ) : (
          <section className="hero-card" aria-labelledby="forbidden-title">
            <span className="status-dot status-dot-warning" />
            <h2 id="forbidden-title">Acceso no autorizado</h2>
            <p>Tu sesión no tiene el permiso necesario para consultar contratos.</p>
          </section>
        )}

      </main>
    </div>
  )
}

export function App() {
  const { theme, loading: brandingLoading, refresh: refreshBranding, reset: resetBranding } = useBranding()
  const auth = useAuthSession()

  useEffect(() => {
    if (auth.session?.tenant?.tenantCode) void refreshBranding()
    else if (auth.status === 'anonymous' || auth.status === 'needsTenant') resetBranding()
  }, [auth.session?.tenant?.tenantCode, auth.status, refreshBranding, resetBranding])

  if (auth.status === 'loading') {
    return <LoadingView />
  }

  if (auth.status === 'error') {
    return (
      <main className="centered-page">
        <section className="auth-card" aria-labelledby="error-title">
          <h1 id="error-title">No fue posible cargar la sesión</h1>
          <p>{auth.error}</p>
          <button type="button" onClick={() => void auth.refresh()}>Reintentar</button>
        </section>
      </main>
    )
  }

  if (auth.status === 'anonymous' || !auth.session) {
    return <LoginView theme={theme} busy={auth.busy} error={auth.error} onLogin={auth.login} />
  }

  if (auth.status === 'needsTenant') {
    return (
      <TenantSelector
        theme={theme}
        session={auth.session}
        busy={auth.busy}
        error={auth.error}
        onSelect={auth.selectTenant}
      />
    )
  }

  return (
    <ApplicationView
      theme={theme}
      session={auth.session}
      brandingLoading={brandingLoading}
      onLogout={auth.logout}
      onUnauthorized={auth.handleUnauthorized}
    />
  )
}

export default App
