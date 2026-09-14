import type { ProblemDetails } from '../types/product'

/**
 * An error carrying the server's RFC 7807 problem details.
 *
 * A plain `Error` with a string message throws away the per-field validation
 * failures the API went to the trouble of returning. Keeping them structured
 * means the form can show "SKU is required" next to the SKU input instead of a
 * generic banner.
 */
export class ApiError extends Error {
  readonly status: number
  readonly problem?: ProblemDetails

  constructor(status: number, message: string, problem?: ProblemDetails) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.problem = problem
  }

  /** Field-level validation messages, keyed by camelCased field name. */
  get fieldErrors(): Record<string, string> {
    const errors = this.problem?.errors
    if (!errors) return {}

    return Object.entries(errors).reduce<Record<string, string>>(
      (accumulator, [field, messages]) => {
        // The API returns PascalCase keys ("Name"); React state uses camelCase.
        const key = field.charAt(0).toLowerCase() + field.slice(1)
        accumulator[key] = messages.join(' ')
        return accumulator
      },
      {},
    )
  }

  /** True when the request failed because the caller is not authenticated. */
  get isUnauthorised(): boolean {
    return this.status === 401
  }

  /** A message suitable for showing to a user. */
  get userMessage(): string {
    if (this.status === 401) return 'Your session has expired. Please sign in again.'
    if (this.status === 409) return this.problem?.detail ?? 'That item already exists.'
    if (this.status === 429) return 'Too many requests. Please wait a moment and try again.'
    if (this.status === 0) return 'Could not reach the API. Is it running?'
    return this.problem?.detail ?? this.problem?.title ?? this.message
  }
}
