import { describe, expect, it, vi } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { ProductForm } from '../ProductForm'

function renderForm(overrides: Partial<React.ComponentProps<typeof ProductForm>> = {}) {
  const onSubmit = overrides.onSubmit ?? vi.fn().mockResolvedValue(undefined)
  render(<ProductForm {...overrides} onSubmit={onSubmit} />)
  return { onSubmit, user: userEvent.setup() }
}

async function fillValidForm(user: ReturnType<typeof userEvent.setup>) {
  await user.type(screen.getByLabelText(/^name/i), 'Ergonomic Desk Lamp')
  await user.type(screen.getByLabelText(/^sku/i), 'LAMP-001')
  await user.type(screen.getByLabelText(/price/i), '49.99')
}

describe('ProductForm', () => {
  it('submits a valid product with trimmed values', async () => {
    const { onSubmit, user } = renderForm()

    await fillValidForm(user)
    await user.selectOptions(screen.getByLabelText(/colour/i), 'Blue')
    await user.click(screen.getByRole('button', { name: /create product/i }))

    await waitFor(() => expect(onSubmit).toHaveBeenCalledTimes(1))

    expect(onSubmit).toHaveBeenCalledWith({
      name: 'Ergonomic Desk Lamp',
      description: null,
      colour: 'Blue',
      price: 49.99,
      sku: 'LAMP-001',
    })
  })

  it('does not call the API when required fields are empty', async () => {
    const { onSubmit, user } = renderForm()

    await user.click(screen.getByRole('button', { name: /create product/i }))

    // Client-side validation exists to save a round trip; if it lets an empty
    // form through it is doing nothing useful.
    expect(onSubmit).not.toHaveBeenCalled()
    expect(await screen.findByText('Product name is required.')).toBeInTheDocument()
    expect(screen.getByText('SKU is required.')).toBeInTheDocument()
    expect(screen.getByText('Price is required.')).toBeInTheDocument()
  })

  it('rejects a negative price', async () => {
    const { onSubmit, user } = renderForm()

    await user.type(screen.getByLabelText(/^name/i), 'Test')
    await user.type(screen.getByLabelText(/^sku/i), 'TEST-1')
    await user.type(screen.getByLabelText(/price/i), '-5')
    await user.click(screen.getByRole('button', { name: /create product/i }))

    expect(await screen.findByText('Price must be zero or greater.')).toBeInTheDocument()
    expect(onSubmit).not.toHaveBeenCalled()
  })

  it('rejects a price with more than two decimal places', async () => {
    const { onSubmit, user } = renderForm()

    await user.type(screen.getByLabelText(/^name/i), 'Test')
    await user.type(screen.getByLabelText(/^sku/i), 'TEST-1')
    await user.type(screen.getByLabelText(/price/i), '10.005')
    await user.click(screen.getByRole('button', { name: /create product/i }))

    expect(
      await screen.findByText('Price must have no more than two decimal places.'),
    ).toBeInTheDocument()
    expect(onSubmit).not.toHaveBeenCalled()
  })

  it('accepts a price of zero', async () => {
    const { onSubmit, user } = renderForm()

    await user.type(screen.getByLabelText(/^name/i), 'Free Sample')
    await user.type(screen.getByLabelText(/^sku/i), 'FREE-1')
    await user.type(screen.getByLabelText(/price/i), '0')
    await user.click(screen.getByRole('button', { name: /create product/i }))

    // Zero is a legitimate price, matching the API's rule. Rejecting it here
    // would block something the server allows.
    await waitFor(() => expect(onSubmit).toHaveBeenCalledTimes(1))
    expect(onSubmit).toHaveBeenCalledWith(expect.objectContaining({ price: 0 }))
  })

  it.each([
    ['AB', 'SKU must be between 3 and 32 characters.'],
    ['BAD SKU', 'SKU may only contain letters, digits and hyphens.'],
    ['BAD_SKU', 'SKU may only contain letters, digits and hyphens.'],
  ])('rejects the malformed SKU %s', async (sku, expectedMessage) => {
    const { onSubmit, user } = renderForm()

    await user.type(screen.getByLabelText(/^name/i), 'Test')
    await user.type(screen.getByLabelText(/^sku/i), sku)
    await user.type(screen.getByLabelText(/price/i), '10')
    await user.click(screen.getByRole('button', { name: /create product/i }))

    expect(await screen.findByText(expectedMessage)).toBeInTheDocument()
    expect(onSubmit).not.toHaveBeenCalled()
  })

  it('shows validation errors returned by the API against the right field', async () => {
    renderForm({ serverErrors: { sku: 'A product with that SKU already exists.' } })

    expect(
      screen.getByText('A product with that SKU already exists.'),
    ).toBeInTheDocument()
  })

  it('marks invalid inputs for assistive technology', async () => {
    const { user } = renderForm()

    await user.click(screen.getByRole('button', { name: /create product/i }))

    // An error a screen reader cannot perceive is not an error message.
    await waitFor(() =>
      expect(screen.getByLabelText(/^name/i)).toHaveAttribute('aria-invalid', 'true'),
    )
    expect(screen.getByLabelText(/^name/i)).toHaveAttribute('aria-describedby', 'name-error')
  })

  it('clears the form only after a successful submit', async () => {
    const { user } = renderForm()

    await fillValidForm(user)
    await user.click(screen.getByRole('button', { name: /create product/i }))

    await waitFor(() => expect(screen.getByLabelText(/^name/i)).toHaveValue(''))
  })

  it('keeps what the user typed when the submit fails', async () => {
    const onSubmit = vi.fn().mockRejectedValue(new Error('boom'))
    const { user } = renderForm({ onSubmit })

    await fillValidForm(user)
    await user.click(screen.getByRole('button', { name: /create product/i }))

    // Losing a filled-in form because the network hiccuped is the kind of
    // detail that makes an app feel broken.
    await waitFor(() =>
      expect(screen.getByLabelText(/^name/i)).toHaveValue('Ergonomic Desk Lamp'),
    )
  })

  it('disables the submit button while a request is in flight', () => {
    renderForm({ isSubmitting: true })

    expect(screen.getByRole('button', { name: /creating/i })).toBeDisabled()
  })
})
