import React from 'react'
import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'
import { BrandLogo } from '../../src/App'
import { defaultTheme } from '../../src/shared/branding/brandTheme'

afterEach(() => cleanup())

describe('BrandLogo', () => {
  it('renders the Toyota PNG with the required accessible alt text and falls back on error', () => {
    render(
      <BrandLogo
        theme={{
          ...defaultTheme,
          tenantCode: 'TOYOTA',
          productName: 'uCredit-auto',
          customerName: 'Toyota Financial Services',
          logoUrl: '/branding/toyota/logo.png',
          browserTitle: 'uCredit-auto | Toyota Financial Services',
        }}
      />,
    )

    const image = screen.getByRole('img', { name: 'Toyota Financial Services' })
    expect(image.getAttribute('src')).toBe('/branding/toyota/logo.png')

    fireEvent.error(image)

    expect(screen.queryByRole('img')).toBeNull()
    expect(screen.getByText('uCredit-auto')).toBeTruthy()
  })
})
