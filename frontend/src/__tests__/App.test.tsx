import { beforeEach, describe, expect, it, vi } from 'vitest'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import App from '../App'
import { tokenStorage } from '../services/tokenStorage'
import type { Product } from '../types/product'

function product(overrides: Partial<Product> = {}): Product {
  return {
    id: crypto.randomUUID(),
    name: 'Ergonomic Desk Lamp',
    description: null,
    colour: 'Red',
    price: 49.99,
    currency: 'GBP',
    sku: 'LAMP-001',
    createdAt: '2026-09-14T10:30:00+00:00',
    updatedAt: '2026-09-14T10:30:00+00:00',
    ...overrides,
  }
}

function pageOf(items: Product[]) {
  return {
    items,
    page: 1,
    pageSize: 100,
    totalCount: items.length,
    totalPages: items.length === 0 ? 0 : 1,
    hasNextPage: false,
    hasPreviousPage: false,
  }
}

function jsonResponse(body: unknown, status = 200) {
  return {
    ok: status >= 200 && status < 300,
    status,
    headers: new Headers({ 'content-type': 'application/json' }),
    json: async () => body,
  } as Response
}

/** Signs in up front, so tests start on the catalogue rather than the sign-in panel. */
function givenSignedIn() {
  tokenStorage.save('test-token', new Date(Date.now() + 600_000).toISOString())
}

describe('App', () => {
  beforeEach(() => {
    tokenStorage.clear()
  })

  it('shows the sign-in panel when there is no token', () => {
    vi.stubGlobal('fetch', vi.fn())

    render(<App />)

    expect(screen.getByRole('button', { name: /^sign in$/i })).toBeInTheDocument()
  })

  it('loads and lists products once signed in', async () => {
    givenSignedIn()
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue(jsonResponse(pageOf([product({ name: 'Desk Lamp' })]))),
    )

    render(<App />)

    expect(await screen.findByText('Desk Lamp')).toBeInTheDocument()
  })

  it('shows an explicit empty state rather than a blank screen', async () => {
    givenSignedIn()
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(jsonResponse(pageOf([]))))

    render(<App />)

    expect(await screen.findByTestId('state-empty')).toBeInTheDocument()
    expect(screen.getByText(/no products yet/i)).toBeInTheDocument()
  })

  it('shows an error state with a retry when loading fails', async () => {
    givenSignedIn()
    const fetchMock = vi.fn().mockRejectedValue(new TypeError('Failed to fetch'))
    vi.stubGlobal('fetch', fetchMock)

    render(<App />)

    // A failed load must say so. Leaving the region blank hides the failure.
    expect(await screen.findByTestId('state-error')).toBeInTheDocument()

    fetchMock.mockResolvedValue(jsonResponse(pageOf([product({ name: 'Recovered' })])))
    await userEvent.click(screen.getByRole('button', { name: /try again/i }))

    expect(await screen.findByText('Recovered')).toBeInTheDocument()
  })

  it('requests the filtered endpoint when a colour is chosen', async () => {
    givenSignedIn()
    const fetchMock = vi.fn().mockResolvedValue(jsonResponse(pageOf([product()])))
    vi.stubGlobal('fetch', fetchMock)

    render(<App />)
    await screen.findByText('Ergonomic Desk Lamp')

    await userEvent.selectOptions(screen.getByLabelText(/^colour$/i), 'Blue')

    await waitFor(() => {
      const urls = fetchMock.mock.calls.map((call) => String(call[0]))
      expect(urls.some((url) => url.includes('colour=Blue'))).toBe(true)
    })
  })

  it('reports an empty filter result distinctly from an empty catalogue', async () => {
    givenSignedIn()
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce(jsonResponse(pageOf([product()])))
      .mockResolvedValue(jsonResponse(pageOf([])))
    vi.stubGlobal('fetch', fetchMock)

    render(<App />)
    await screen.findByText('Ergonomic Desk Lamp')

    await userEvent.selectOptions(screen.getByLabelText(/^colour$/i), 'Gold')

    // "No gold products" tells the user the filter is the reason; "no products
    // yet" would be actively misleading here.
    expect(await screen.findByText(/no gold products/i)).toBeInTheDocument()
  })

  it('signs out and returns to the sign-in panel when the API rejects the token', async () => {
    givenSignedIn()
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue(jsonResponse({ status: 401, title: 'Unauthorized' }, 401)),
    )

    render(<App />)

    // An expired token should send the user back to sign in, not strand them
    // on an endlessly failing screen.
    expect(await screen.findByRole('button', { name: /^sign in$/i })).toBeInTheDocument()
  })

  it('creates a product and refreshes the list', async () => {
    givenSignedIn()
    const created = product({ name: 'New Widget', sku: 'NEW-001' })

    const fetchMock = vi.fn().mockImplementation((_url: string, init?: RequestInit) => {
      if (init?.method === 'POST') return Promise.resolve(jsonResponse(created, 201))
      return Promise.resolve(jsonResponse(pageOf([created])))
    })
    vi.stubGlobal('fetch', fetchMock)

    render(<App />)
    await screen.findByText('New Widget')

    const form = screen.getByRole('form', { name: /create product/i })
    await userEvent.type(within(form).getByLabelText(/^name/i), 'New Widget')
    await userEvent.type(within(form).getByLabelText(/^sku/i), 'NEW-001')
    await userEvent.type(within(form).getByLabelText(/price/i), '19.99')
    await userEvent.click(within(form).getByRole('button', { name: /create product/i }))

    const banner = await screen.findByRole('status')
    expect(banner).toHaveTextContent(/Created .New Widget./)
  })

  it('attaches API validation errors to the form fields', async () => {
    givenSignedIn()

    const fetchMock = vi.fn().mockImplementation((_url: string, init?: RequestInit) => {
      if (init?.method === 'POST') {
        return Promise.resolve(
          jsonResponse(
            { status: 400, errors: { Sku: ['A product with that SKU already exists.'] } },
            400,
          ),
        )
      }
      return Promise.resolve(jsonResponse(pageOf([])))
    })
    vi.stubGlobal('fetch', fetchMock)

    render(<App />)
    await screen.findByTestId('state-empty')

    const form = screen.getByRole('form', { name: /create product/i })
    await userEvent.type(within(form).getByLabelText(/^name/i), 'Widget')
    await userEvent.type(within(form).getByLabelText(/^sku/i), 'DUP-001')
    await userEvent.type(within(form).getByLabelText(/price/i), '19.99')
    await userEvent.click(within(form).getByRole('button', { name: /create product/i }))

    expect(
      await screen.findByText('A product with that SKU already exists.'),
    ).toBeInTheDocument()

    // The user's input survives the failure so they can correct just the SKU.
    expect(within(form).getByLabelText(/^name/i)).toHaveValue('Widget')
  })
})
