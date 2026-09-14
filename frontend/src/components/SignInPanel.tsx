import { useState, type FormEvent } from 'react'

interface SignInPanelProps {
  onSignIn: (username: string, password: string) => Promise<void>
  error?: string
  isSubmitting?: boolean
}

/**
 * Demo sign-in.
 *
 * Pre-filled with the demo credentials on purpose: the point of this panel is
 * to show a reviewer how the bearer token is obtained and attached, not to
 * simulate a real login. The fields stay editable so a wrong-password path can
 * be exercised too.
 */
export function SignInPanel({ onSignIn, error, isSubmitting }: SignInPanelProps) {
  const [username, setUsername] = useState('demo')
  const [password, setPassword] = useState('Password123!')

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    await onSignIn(username, password)
  }

  return (
    <div className="mx-auto max-w-md rounded-xl border border-slate-200 bg-white p-8 shadow-sm">
      <h2 className="text-lg font-semibold text-slate-900">Sign in</h2>

      <p className="mt-1 text-sm text-slate-600">
        The API requires a bearer token. This exchanges the demo credentials for a short-lived
        JWT, standing in for a real identity provider.
      </p>

      <form onSubmit={handleSubmit} className="mt-6 space-y-4">
        <div>
          <label htmlFor="username" className="mb-1 block text-sm font-medium text-slate-700">
            Username
          </label>
          <input
            id="username"
            type="text"
            autoComplete="username"
            value={username}
            onChange={(e) => setUsername(e.target.value)}
            className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm shadow-sm focus:border-slate-500 focus:outline-none focus:ring-1 focus:ring-slate-500"
          />
        </div>

        <div>
          <label htmlFor="password" className="mb-1 block text-sm font-medium text-slate-700">
            Password
          </label>
          <input
            id="password"
            type="password"
            autoComplete="current-password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm shadow-sm focus:border-slate-500 focus:outline-none focus:ring-1 focus:ring-slate-500"
          />
        </div>

        {error && (
          <p role="alert" className="rounded-md bg-red-50 px-3 py-2 text-sm text-red-700">
            {error}
          </p>
        )}

        <button
          type="submit"
          disabled={isSubmitting}
          className="w-full rounded-md bg-slate-900 px-4 py-2.5 text-sm font-semibold text-white transition hover:bg-slate-700 focus:outline-none focus-visible:ring-2 focus-visible:ring-slate-500 focus-visible:ring-offset-2 disabled:opacity-60"
        >
          {isSubmitting ? 'Signing in…' : 'Sign in'}
        </button>
      </form>
    </div>
  )
}
