const STORAGE_KEY = 'products.demo.token'

interface StoredToken {
  accessToken: string
  expiresAt: string
}

/**
 * Keeps the demo bearer token in `localStorage` so a page refresh does not
 * force another sign-in.
 *
 * A production app would not store a bearer token in `localStorage`: anything
 * running on the page can read it, so any XSS becomes credential theft. The
 * usual answer is a short-lived token held in memory alongside a refresh token
 * in an `HttpOnly` cookie, which JavaScript cannot read at all. This is a
 * deliberate simplification for a reviewable demo, and it is called out in the
 * README rather than left to be discovered.
 */
export const tokenStorage = {
  save(accessToken: string, expiresAt: string): void {
    try {
      const payload: StoredToken = { accessToken, expiresAt }
      localStorage.setItem(STORAGE_KEY, JSON.stringify(payload))
    } catch {
      // Private browsing and blocked site data both make localStorage throw.
      // Losing persistence is survivable; crashing the app is not.
    }
  },

  /** Returns the stored token, or null if absent, malformed or expired. */
  load(): string | null {
    try {
      const raw = localStorage.getItem(STORAGE_KEY)
      if (!raw) return null

      const parsed = JSON.parse(raw) as StoredToken
      if (!parsed?.accessToken || !parsed?.expiresAt) return null

      // Discard a token that has already expired, so the app asks for a new one
      // up front instead of sending a request it knows will 401.
      if (new Date(parsed.expiresAt).getTime() <= Date.now()) {
        this.clear()
        return null
      }

      return parsed.accessToken
    } catch {
      return null
    }
  },

  clear(): void {
    try {
      localStorage.removeItem(STORAGE_KEY)
    } catch {
      // Nothing useful to do if storage is unavailable.
    }
  },
}
