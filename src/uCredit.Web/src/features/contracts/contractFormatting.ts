const unavailable = 'No disponible'

export function formatAmount(value: number | null, currency: string | null): string {
  if (value === null || value === undefined) {
    return unavailable
  }

  if (currency) {
    try {
      return new Intl.NumberFormat('es-MX', {
        style: 'currency',
        currency,
        minimumFractionDigits: 2,
        maximumFractionDigits: 2,
      }).format(value)
    } catch {
      // Fall through to a locale-aware number when Legacy returns an unknown currency code.
    }
  }

  return `${new Intl.NumberFormat('es-MX', {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  }).format(value)} (moneda no disponible)`
}

export function formatDate(value: string | null): string {
  if (!value) {
    return unavailable
  }

  const match = /^(\d{4})-(\d{2})-(\d{2})/.exec(value)
  if (!match) {
    return unavailable
  }

  const date = new Date(Number(match[1]), Number(match[2]) - 1, Number(match[3]))
  if (Number.isNaN(date.getTime())) {
    return unavailable
  }

  return new Intl.DateTimeFormat('es-MX', { dateStyle: 'medium' }).format(date)
}

export function formatText(value: string | number | null | undefined): string {
  return value === null || value === undefined || value === '' ? unavailable : String(value)
}
export function formatCurrency(code: string | null, name: string | null): string {
  if (!code && !name) {
    return unavailable
  }

  if (!code) {
    return name ?? unavailable
  }

  return name ? `${code} — ${name}` : code
}