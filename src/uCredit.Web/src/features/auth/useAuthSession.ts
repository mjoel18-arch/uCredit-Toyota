import { useCallback, useEffect, useState } from 'react'
import { apiErrorMessage, ApiError } from '../../shared/api/apiClient'
import {
  getSession,
  login as loginRequest,
  logout as logoutRequest,
  selectTenant as selectTenantRequest,
  type AuthSession,
} from './authApi'

export type AuthStatus = 'loading' | 'anonymous' | 'needsTenant' | 'authenticated' | 'error'

export function useAuthSession() {
  const [session, setSession] = useState<AuthSession | null>(null)
  const [status, setStatus] = useState<AuthStatus>('loading')
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)

  const refresh = useCallback(async (): Promise<AuthSession | null> => {
    try {
      const current = await getSession()
      setSession(current)
      setStatus(current.tenant ? 'authenticated' : 'needsTenant')
      setError('')
      return current
    } catch (requestError) {
      setSession(null)
      if (requestError instanceof ApiError && requestError.status === 401) {
        setStatus('anonymous')
      } else {
        setStatus('error')
        setError(apiErrorMessage(requestError, 'No fue posible cargar la sesión.'))
      }
      return null
    }
  }, [])

  useEffect(() => {
    window.setTimeout(() => { void refresh() }, 0)
  }, [refresh])

  const login = useCallback(async (userName: string, password: string) => {
    setBusy(true)
    setError('')
    try {
      await loginRequest(userName, password)
      await refresh()
    } catch (requestError) {
      setSession(null)
      setStatus('anonymous')
      setError(apiErrorMessage(requestError, 'No fue posible iniciar sesión.'))
    } finally {
      setBusy(false)
    }
  }, [refresh])

  const selectTenant = useCallback(async (tenantCode: string) => {
    setBusy(true)
    setError('')
    try {
      await selectTenantRequest(tenantCode)
      await refresh()
    } catch (requestError) {
      setError(apiErrorMessage(requestError, 'No fue posible seleccionar el tenant.'))
    } finally {
      setBusy(false)
    }
  }, [refresh])

  const logout = useCallback(async () => {
    setBusy(true)
    try {
      await logoutRequest()
    } catch {
      // The local session is cleared even if the server already considers it logged out.
    } finally {
      setSession(null)
      setStatus('anonymous')
      setError('')
      setBusy(false)
    }
  }, [])

  const handleUnauthorized = useCallback(() => {
    setSession(null)
    setStatus('anonymous')
    setError('La sesión no es válida o ya expiró.')
  }, [])

  return {
    session,
    status,
    error,
    busy,
    login,
    selectTenant,
    logout,
    refresh,
    handleUnauthorized,
  }
}