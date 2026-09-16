import { useState, type FormEvent } from 'react'
import { useBranding } from './shared/branding/BrandingProvider'
import { ContractsView } from './features/contracts/ContractsView'
import { useAuthSession } from './features/auth/useAuthSession'
import type { AuthSession } from './features/auth/authApi'
import type { BrandTheme } from './shared/branding/brandTheme'

function LoadingView({ label = 'Cargando…' }: { label?: string }) {
  return <main className="centered-page" aria-live="polite"><div className="loading-panel">{label}</div></main>
}

function LoginView({
  applicationName,
  busy,
  error,
  onLogin,
}: {
  applicationName: string
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
        <span className="eyebrow">{applicationName}</span>
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
  applicationName,
  session,
  busy,
  error,
  onSelect,
}: {
  applicationName: string
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
        <span className="eyebrow">{applicationName} · sesión autenticada</span>
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

  return (
    <div className="app-shell">
      <aside className="sidebar">
        <div className="brand">
          {theme.logoUrl ? <img src={theme.logoUrl} alt={'Logotipo de ' + theme.applicationName} /> : <span className="brand-mark">uC</span>}
          <div>
            <strong>{theme.applicationName}</strong>
            <small>{brandingLoading ? 'Cargando identidad…' : session.tenant?.tenantCode}</small>
          </div>
        </div>
        <nav aria-label="Navegación principal">
          <a className={hasContractsPermission ? 'active' : ''} href="#contracts">Contratos</a>
          <a href="#people" aria-disabled="true">Personas</a>
          <a href="#collections" aria-disabled="true">Cobranza</a>
          <a href="#reports" aria-disabled="true">Reportes</a>
        </nav>
      </aside>

      <main>
        <header className="topbar">
          <div>
            <span className="eyebrow">Primer vertical</span>
            <h1>{session.tenant?.tenantCode ?? 'Sesión activa'}</h1>
          </div>
          <div className="session-actions">
            <span className="user-chip" aria-label={'Usuario actual: ' + session.userName}>{initials(session.userName)}</span>
            <span className="session-user">{session.userName}</span>
            <button type="button" className="button-secondary" onClick={() => void onLogout()}>Cerrar sesión</button>
          </div>
        </header>

        {hasContractsPermission ? (
          <ContractsView onUnauthorized={onUnauthorized} />
        ) : (
          <section className="hero-card" aria-labelledby="forbidden-title">
            <span className="status-dot status-dot-warning" />
            <h2 id="forbidden-title">Acceso no autorizado</h2>
            <p>Tu sesión no tiene el permiso necesario para consultar contratos.</p>
          </section>
        )}

        <section className="metrics" aria-label="Resumen del módulo">
          <article><span>Arquitectura</span><strong>Modular</strong><small>Separación por capacidades</small></article>
          <article><span>Acceso</span><strong>Lectura</strong><small>Sin cambios en Legacy</small></article>
          <article><span>Seguridad</span><strong>Cookie segura</strong><small>Sesión protegida por Identity</small></article>
        </section>
      </main>
    </div>
  )
}

export function App() {
  const { theme, loading: brandingLoading } = useBranding()
  const auth = useAuthSession()

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
    return <LoginView applicationName={theme.applicationName} busy={auth.busy} error={auth.error} onLogin={auth.login} />
  }

  if (auth.status === 'needsTenant') {
    return (
      <TenantSelector
        applicationName={theme.applicationName}
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