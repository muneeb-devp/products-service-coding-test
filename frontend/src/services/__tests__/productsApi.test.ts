import { beforeEach, describe, expect, it, vi } from 'vitest'
import { productsApi } from '../productsApi'
import { authApi } from '../authApi'
import { ApiError } from '../apiError'
import { tokenStorage } from '../tokenStorage'

function mockFetch(response: Partial<Response> & { jsonBody?: unknown }) {
  const fetchMock = vi.fn().mockResolvedValue({
    ok: response.ok ?? true,
    status: response.status ?? 200,
    headers: new Headers({ 'content-type': 'application/json' }),
    json: async () => response.jsonBody,
    ...response,
  } as Response)

  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}

const emptyPage = {
  items: [],
  page: 1,
  pageSize: 20,
  totalCount: 0,
  totalPages: 0,
  hasNextPage: false,
  hasPreviousPage: false,
}

describe('productsApi', () => {
  beforeEach(() => {
    tokenStorage.clear()
  })

  it('requests the unfiltered list with no query string', async () => {
    const fetchMock = mockFetch({ jsonBody: emptyPage })

    await productsApi.list()

    expect(fetchMock).toHaveBeenCalledWith(
      expect.stringContaining('/api/products'),
      expect.objectContaining({ method: 'GET' }),
    )
    expect(fetchMock.mock.calls[0][0]).not.toContain('colour=')
  })

  it('passes the colour filter as a query parameter', async () => {
    const fetchMock = mockFetch({ jsonBody: emptyPage })

    await productsApi.list({ colour: 'Red' })

    expect(fetchMock.mock.calls[0][0]).toContain('colour=Red')
  })

  it('omits empty filter values rather than sending blanks', async () => {
    const fetchMock = mockFetch({ jsonBody: emptyPage })

    await productsApi.list({ colour: null, page: 1 })

    const url = fetchMock.mock.calls[0][0] as string
    expect(url).not.toContain('colour=')
    expect(url).toContain('page=1')
  })

  it('attaches the stored bearer token', async () => {
    tokenStorage.save('test-token', new Date(Date.now() + 60_000).toISOString())
    const fetchMock = mockFetch({ jsonBody: emptyPage })

    await productsApi.list()

    const init = fetchMock.mock.calls[0][1] as RequestInit
    expect((init.headers as Record<string, string>).Authorization).toBe('Bearer test-token')
  })

  it('sends no Authorization header when there is no token', async () => {
    const fetchMock = mockFetch({ jsonBody: emptyPage })

    await productsApi.list()

    const init = fetchMock.mock.calls[0][1] as RequestInit
    expect((init.headers as Record<string, string>).Authorization).toBeUndefined()
  })

  it('surfaces validation problems as an ApiError with field errors', async () => {
    mockFetch({
      ok: false,
      status: 400,
      jsonBody: {
        status: 400,
        title: 'One or more validation errors occurred.',
        errors: { Name: ['Product name is required.'], Sku: ['SKU is required.'] },
      },
    })

    const error = await productsApi
      .create({ name: '', colour: 'Red', price: 1, sku: '' })
      .catch((e: unknown) => e)

    expect(error).toBeInstanceOf(ApiError)

    // PascalCase from the API is mapped to the camelCase field names the form
    // uses, so each message lands on the right input.
    expect((error as ApiError).fieldErrors).toEqual({
      name: 'Product name is required.',
      sku: 'SKU is required.',
    })
  })

  it('reports a conflict with the server’s explanation', async () => {
    mockFetch({
      ok: false,
      status: 409,
      jsonBody: { status: 409, title: 'Conflict', detail: "SKU 'LAMP-001' already exists." },
    })

    const error = (await productsApi
      .create({ name: 'x', colour: 'Red', price: 1, sku: 'LAMP-001' })
      .catch((e: unknown) => e)) as ApiError

    expect(error.status).toBe(409)
    expect(error.userMessage).toContain('LAMP-001')
  })

  it('treats a network failure distinctly from an HTTP error', async () => {
    vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new TypeError('Failed to fetch')))

    const error = (await productsApi.list().catch((e: unknown) => e)) as ApiError

    // Status 0 marks "never reached the server", which deserves a different
    // message from any response the server actually sent.
    expect(error.status).toBe(0)
    expect(error.userMessage).toContain('Could not reach the API')
  })

  it('handles a 204 No Content response without trying to parse a body', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue({
        ok: true,
        status: 204,
        headers: new Headers(),
        json: async () => {
          throw new Error('should not be called')
        },
      } as unknown as Response),
    )

    await expect(productsApi.remove('some-id')).resolves.toBeUndefined()
  })
})

describe('authApi', () => {
  beforeEach(() => tokenStorage.clear())

  it('stores the token returned by the token endpoint', async () => {
    mockFetch({
      jsonBody: {
        accessToken: 'issued-token',
        tokenType: 'Bearer',
        expiresAt: new Date(Date.now() + 600_000).toISOString(),
        expiresInSeconds: 600,
      },
    })

    await authApi.signIn('demo', 'Password123!')

    expect(authApi.isSignedIn()).toBe(true)
    expect(tokenStorage.load()).toBe('issued-token')
  })

  it('requests a token without sending one', async () => {
    tokenStorage.save('old-token', new Date(Date.now() + 60_000).toISOString())

    const fetchMock = mockFetch({
      jsonBody: {
        accessToken: 'new-token',
        tokenType: 'Bearer',
        expiresAt: new Date(Date.now() + 600_000).toISOString(),
        expiresInSeconds: 600,
      },
    })

    await authApi.signIn('demo', 'Password123!')

    const init = fetchMock.mock.calls[0][1] as RequestInit
    expect((init.headers as Record<string, string>).Authorization).toBeUndefined()
  })

  it('discards an expired token instead of sending it', () => {
    tokenStorage.save('stale', new Date(Date.now() - 1_000).toISOString())

    // Sending a token we already know is expired just buys a guaranteed 401.
    expect(tokenStorage.load()).toBeNull()
    expect(authApi.isSignedIn()).toBe(false)
  })

  it('clears the token on sign out', async () => {
    tokenStorage.save('token', new Date(Date.now() + 60_000).toISOString())

    authApi.signOut()

    expect(authApi.isSignedIn()).toBe(false)
  })
})
