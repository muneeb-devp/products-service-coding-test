import { apiRequest, buildQueryString } from './apiClient'
import type {
  CreateProductRequest,
  PagedResponse,
  Product,
  ProductQuery,
} from '../types/product'

/**
 * Everything this app can ask the Products API to do.
 *
 * Components import from here and never touch `fetch`, so a change to the API's
 * shape is one edit in one file rather than a hunt through the component tree.
 */
export const productsApi = {
  /**
   * Lists products, optionally filtered by colour.
   *
   * The colour filter is a query parameter on the same endpoint rather than a
   * separate call, mirroring the API: "all products" and "red products" are one
   * request shape with one field set or unset.
   */
  async list(query: ProductQuery = {}, signal?: AbortSignal): Promise<PagedResponse<Product>> {
    const queryString = buildQueryString({
      colour: query.colour ?? undefined,
      page: query.page,
      pageSize: query.pageSize,
      sortBy: query.sortBy,
      sortDescending: query.sortDescending,
    })

    return apiRequest<PagedResponse<Product>>(`/api/products${queryString}`, { signal })
  },

  async getById(id: string, signal?: AbortSignal): Promise<Product> {
    return apiRequest<Product>(`/api/products/${id}`, { signal })
  },

  async create(product: CreateProductRequest): Promise<Product> {
    return apiRequest<Product>('/api/products', { method: 'POST', body: product })
  },

  async remove(id: string): Promise<void> {
    return apiRequest<void>(`/api/products/${id}`, { method: 'DELETE' })
  },
}
