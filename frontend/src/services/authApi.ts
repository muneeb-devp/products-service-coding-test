import { apiRequest } from './apiClient'
import { tokenStorage } from './tokenStorage'

interface TokenResponse {
  accessToken: string
  tokenType: string
  expiresAt: string
  expiresInSeconds: number
}

/**
 * Demo authentication.
 *
 * Mirrors the API's stand-in token endpoint: there is no real identity
 * provider here, and this is the client half of that deliberate simplification.
 * A production app would redirect to an IdP and receive tokens through an
 * authorisation-code flow with PKCE.
 */
export const authApi = {
  /** Exchanges demo credentials for a bearer token and stores it. */
  async signIn(username: string, password: string): Promise<void> {
    const token = await apiRequest<TokenResponse>('/api/auth/token', {
      method: 'POST',
      body: { username, password },
      // The token endpoint is how we *get* a token, so it cannot require one.
      authenticated: false,
    })

    tokenStorage.save(token.accessToken, token.expiresAt)
  },

  signOut(): void {
    tokenStorage.clear()
  },

  /** Whether a usable (unexpired) token is currently held. */
  isSignedIn(): boolean {
    return tokenStorage.load() !== null
  },
}
