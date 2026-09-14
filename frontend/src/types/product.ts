/**
 * The colours the API accepts. Kept in lockstep with the ProductColour enum on
 * the server.
 *
 * Declared `as const` and derived into a union type so the compiler rejects a
 * typo'd colour at build time rather than the API rejecting it at run time.
 */
export const PRODUCT_COLOURS = [
  'Red',
  'Green',
  'Blue',
  'Yellow',
  'Orange',
  'Purple',
  'Pink',
  'Brown',
  'Black',
  'White',
  'Grey',
  'Silver',
  'Gold',
] as const

export type ProductColour = (typeof PRODUCT_COLOURS)[number]

/** A product as returned by the API. */
export interface Product {
  id: string
  name: string
  description?: string | null
  colour: ProductColour
  price: number
  currency: string
  sku: string
  createdAt: string
  updatedAt: string
}

/** Request body for creating a product. */
export interface CreateProductRequest {
  name: string
  description?: string | null
  colour: ProductColour
  price: number
  sku: string
  currency?: string | null
}

/** A page of results, mirroring the API's envelope. */
export interface PagedResponse<T> {
  items: T[]
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
  hasNextPage: boolean
  hasPreviousPage: boolean
}

/** Query parameters accepted by the product listing endpoint. */
export interface ProductQuery {
  colour?: ProductColour | null
  page?: number
  pageSize?: number
  sortBy?: string
  sortDescending?: boolean
}

/**
 * An RFC 7807 problem response.
 *
 * `errors` is present on validation failures, keyed by field name, so the form
 * can attach each message to the input that caused it.
 */
export interface ProblemDetails {
  type?: string
  title?: string
  status?: number
  detail?: string
  instance?: string
  traceId?: string
  errors?: Record<string, string[]>
}
