import { ApiError } from './apiError'
import { tokenStorage } from './tokenStorage'
import type { ProblemDetails } from '../types/product'

/**
 * Base URL of the API.
 *
 * Read from the environment rather than hardcoded, so the same bundle can be
 * pointed at a local API, a container, or a deployed environment. See
 * `.env.example`.
 */
export const API_BASE_URL: string = (
  import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5099'
).replace(/\/+$/, '')

interface RequestOptions {
  method?: 'GET' | 'POST' | 'PUT' | 'DELETE'
  body?: unknown
  /** Send the stored bearer token. Defaults to true. */
  authenticated?: boolean
  signal?: AbortSignal
}

/**
 * The single place this app talks to the network.
 *
 * Centralising it means the bearer header, JSON encoding, problem-details
 * parsing and error mapping are written once and behave identically everywhere.
 * Scattering `fetch` through components means each one handles (or forgets to
 * handle) its own errors.
 */
export async function apiRequest<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const { method = 'GET', body, authenticated = true, signal } = options

  const headers: Record<string, string> = { Accept: 'application/json' }

  if (body !== undefined) {
    headers['Content-Type'] = 'application/json'
  }

  if (authenticated) {
    const token = tokenStorage.load()
    if (token) {
      headers.Authorization = `Bearer ${token}`
    }
  }

  let response: Response

  try {
    response = await fetch(`${API_BASE_URL}${path}`, {
      method,
      headers,
      body: body === undefined ? undefined : JSON.stringify(body),
      signal,
    })
  } catch (error) {
    // An aborted request is the caller changing their mind, not a failure.
    // Rethrow so the caller can ignore it instead of showing an error.
    if (error instanceof DOMException && error.name === 'AbortError') {
      throw error
    }

    // fetch rejects only on network-level failures: the API is down, DNS
    // failed, or CORS blocked the request. Status 0 marks that distinction.
    throw new ApiError(0, 'Network request failed.')
  }

  if (response.status === 204) {
    return undefined as T
  }

  const isJson = response.headers
    .get('content-type')
    ?.toLowerCase()
    .includes('json')

  const payload = isJson ? await response.json().catch(() => undefined) : undefined

  if (!response.ok) {
    throw new ApiError(
      response.status,
      `Request failed with status ${response.status}.`,
      payload as ProblemDetails | undefined,
    )
  }

  return payload as T
}

/** Builds a query string, omitting empty values so URLs stay clean. */
export function buildQueryString(params: Record<string, unknown>): string {
  const search = new URLSearchParams()

  for (const [key, value] of Object.entries(params)) {
    if (value === undefined || value === null || value === '') continue
    search.append(key, String(value))
  }

  const query = search.toString()
  return query ? `?${query}` : ''
}
