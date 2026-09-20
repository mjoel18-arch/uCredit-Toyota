export class ApiError extends Error {
  readonly status: number
  readonly code?: string

  constructor(status: number, code?: string) {
    super('API request failed')
    this.name = 'ApiError'
    this.status = status
    this.code = code
  }
}

const configuredBaseUrl = (import.meta.env.VITE_API_BASE_URL as string | undefined)?.trim().replace(/\/+$/, '') ?? ''

export async function apiRequest<T>(path: string, init: RequestInit = {}): Promise<T> {
  const headers = new Headers(init.headers)
  headers.set('Accept', 'application/json')

  if (init.body && !headers.has('Content-Type')) {
    headers.set('Content-Type', 'application/json')
  }

  let response: Response
  try {
    response = await fetch(configuredBaseUrl + path, {
      ...init,
      credentials: 'include',
      headers,
    })
  } catch {
    throw new ApiError(0)
  }

  if (!response.ok) {
    let code: string | undefined
    try {
      const problem = await response.json() as { code?: string; extensions?: { code?: string } }
      code = problem.code ?? problem.extensions?.code
    } catch { /* Keep the status-only error for non-JSON responses. */ }
    throw new ApiError(response.status, code)
  }

  if (response.status === 204) {
    return undefined as T
  }

  try {
    return (await response.json()) as T
  } catch {
    throw new ApiError(500)
  }
}

export function apiErrorMessage(error: unknown, fallback = 'No fue posible completar la operación.'): string {
  if (!(error instanceof ApiError)) {
    return fallback
  }

  switch (error.status) {
    case 400:
      return 'La solicitud no es válida.'
    case 401:
      return 'La sesión no es válida o ya expiró.'
    case 403:
      return 'No tienes autorización para realizar esta operación.'
    case 409:
      return 'Ya existe un cliente registrado con ese RFC; no se guardó información.'
    case 422:
      return 'La solicitud requiere una confirmación o corrección adicional.'
    case 500:
      return 'El servicio no está disponible en este momento.'
    case 503:
      return 'El servicio no está disponible; no se guardó información.'
    default:
      return fallback
  }
}
