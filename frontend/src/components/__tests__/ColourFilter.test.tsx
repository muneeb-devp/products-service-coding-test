import { describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { ColourFilter } from '../ColourFilter'
import { PRODUCT_COLOURS } from '../../types/product'

describe('ColourFilter', () => {
  it('offers every colour the API accepts, plus an unfiltered option', () => {
    render(<ColourFilter value={null} onChange={vi.fn()} />)

    expect(screen.getAllByRole('option')).toHaveLength(PRODUCT_COLOURS.length + 1)
    expect(screen.getByRole('option', { name: 'All colours' })).toBeInTheDocument()
  })

  it('reports the selected colour', async () => {
    const onChange = vi.fn()
    render(<ColourFilter value={null} onChange={onChange} />)

    await userEvent.selectOptions(screen.getByLabelText(/colour/i), 'Blue')

    expect(onChange).toHaveBeenCalledWith('Blue')
  })

  it('reports null when the filter is cleared', async () => {
    const onChange = vi.fn()
    render(<ColourFilter value="Blue" onChange={onChange} />)

    await userEvent.selectOptions(screen.getByLabelText(/colour/i), '')

    // null rather than an empty string, so the API client can omit the
    // parameter entirely instead of sending `colour=`.
    expect(onChange).toHaveBeenCalledWith(null)
  })

  it('can be disabled while a request is in flight', () => {
    render(<ColourFilter value={null} onChange={vi.fn()} disabled />)

    expect(screen.getByLabelText(/colour/i)).toBeDisabled()
  })
})
