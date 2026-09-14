import { describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { ProductTable } from '../ProductTable'
import type { Product } from '../../types/product'

const product: Product = {
  id: '11111111-1111-1111-1111-111111111111',
  name: 'Ergonomic Desk Lamp',
  description: 'Adjustable LED lamp.',
  colour: 'Red',
  price: 49.99,
  currency: 'GBP',
  sku: 'LAMP-001',
  createdAt: '2026-09-14T10:30:00+00:00',
  updatedAt: '2026-09-14T10:30:00+00:00',
}

describe('ProductTable', () => {
  it('renders a row per product', () => {
    render(
      <ProductTable
        products={[product, { ...product, id: 'b', name: 'Second', sku: 'SKU-2' }]}
      />,
    )

    expect(screen.getByText('Ergonomic Desk Lamp')).toBeInTheDocument()
    expect(screen.getByText('Second')).toBeInTheDocument()
    // One header row plus two data rows.
    expect(screen.getAllByRole('row')).toHaveLength(3)
  })

  it('formats the price as currency', () => {
    render(<ProductTable products={[product]} />)

    expect(screen.getByText('£49.99')).toBeInTheDocument()
  })

  it('falls back gracefully for an unrecognised currency code', () => {
    // An unknown currency must not take the whole table down with it.
    render(<ProductTable products={[{ ...product, currency: 'ZZZ' }]} />)

    expect(screen.getByText(/49\.00|49\.99/)).toBeInTheDocument()
  })

  it('gives each delete button an accessible name naming the product', async () => {
    const onDelete = vi.fn()
    render(<ProductTable products={[product]} onDelete={onDelete} />)

    const button = screen.getByRole('button', { name: 'Delete Ergonomic Desk Lamp' })
    await userEvent.click(button)

    expect(onDelete).toHaveBeenCalledWith(product)
  })

  it('omits the actions column when deletion is not offered', () => {
    render(<ProductTable products={[product]} />)

    expect(screen.queryByRole('button', { name: /delete/i })).not.toBeInTheDocument()
  })

  it('disables only the row being deleted', () => {
    const second = { ...product, id: 'b', name: 'Second', sku: 'SKU-2' }
    render(
      <ProductTable products={[product, second]} onDelete={vi.fn()} deletingId={product.id} />,
    )

    expect(screen.getByRole('button', { name: /Delete Ergonomic Desk Lamp/ })).toBeDisabled()
    expect(screen.getByRole('button', { name: 'Delete Second' })).toBeEnabled()
  })
})
