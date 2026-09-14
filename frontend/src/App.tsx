import { useCallback, useEffect, useState } from 'react'
import { ColourFilter } from './components/ColourFilter'
import { ProductForm } from './components/ProductForm'
import { ProductTable } from './components/ProductTable'
import { SignInPanel } from './components/SignInPanel'
import { StateMessage } from './components/StateMessage'
import { ApiError } from './services/apiError'
import { authApi } from './services/authApi'
import { productsApi } from './services/productsApi'
import type { CreateProductRequest, Product, ProductColour } from './types/product'

/**
 * Explicit request states.
 *
 * A boolean `isLoading` cannot express "loaded, but empty" or "failed" without
 * a second and third flag, and those flags drift out of sync. One field with
 * four values makes every state reachable and mutually exclusive.
 */
type LoadState = 'idle' | 'loading' | 'loaded' | 'error'

export default function App() {
  const [isSignedIn, setIsSignedIn] = useState(() => authApi.isSignedIn())
  const [signInError, setSignInError] = useState<string>()
  const [isSigningIn, setIsSigningIn] = useState(false)

  const [products, setProducts] = useState<Product[]>([])
  const [loadState, setLoadState] = useState<LoadState>('idle')
  const [loadError, setLoadError] = useState<string>()
  const [totalCount, setTotalCount] = useState(0)

  const [colour, setColour] = useState<ProductColour | null>(null)

  const [isCreating, setIsCreating] = useState(false)
  const [createErrors, setCreateErrors] = useState<Record<string, string>>({})
  const [banner, setBanner] = useState<string>()
  const [deletingId, setDeletingId] = useState<string | null>(null)

  const loadProducts = useCallback(
    async (signal?: AbortSignal) => {
      setLoadState('loading')
      setLoadError(undefined)

      try {
        const page = await productsApi.list({ colour, pageSize: 100 }, signal)

        setProducts(page.items)
        setTotalCount(page.totalCount)
        setLoadState('loaded')
      } catch (error) {
        // An aborted request means the filter changed and a newer request is
        // already in flight, not a failure to report.
        if (error instanceof DOMException && error.name === 'AbortError') return

        if (error instanceof ApiError && error.isUnauthorised) {
          authApi.signOut()
          setIsSignedIn(false)
          return
        }

        setLoadError(error instanceof ApiError ? error.userMessage : 'Something went wrong.')
        setLoadState('error')
      }
    },
    [colour],
  )

  useEffect(() => {
    if (!isSignedIn) return

    // Aborting the previous request prevents a slow response for an old filter
    // from overwriting a fast response for the current one.
    const controller = new AbortController()
    void loadProducts(controller.signal)

    return () => controller.abort()
  }, [isSignedIn, loadProducts])

  async function handleSignIn(username: string, password: string) {
    setIsSigningIn(true)
    setSignInError(undefined)

    try {
      await authApi.signIn(username, password)
      setIsSignedIn(true)
    } catch (error) {
      setSignInError(
        error instanceof ApiError ? error.userMessage : 'Could not sign in. Please try again.',
      )
    } finally {
      setIsSigningIn(false)
    }
  }

  async function handleCreate(product: CreateProductRequest) {
    setIsCreating(true)
    setCreateErrors({})
    setBanner(undefined)

    try {
      const created = await productsApi.create(product)

      setBanner(`Created "${created.name}".`)
      await loadProducts()
    } catch (error) {
      if (error instanceof ApiError) {
        // Field-level messages go back to the inputs that caused them; anything
        // without a field (a duplicate SKU, say) surfaces as a banner.
        const fieldErrors = error.fieldErrors

        if (Object.keys(fieldErrors).length > 0) {
          setCreateErrors(fieldErrors)
        } else {
          setBanner(error.userMessage)
        }

        // Rethrow so the form knows not to clear what the user typed.
        throw error
      }

      setBanner('Could not create the product.')
      throw error
    } finally {
      setIsCreating(false)
    }
  }

  async function handleDelete(product: Product) {
    setDeletingId(product.id)

    try {
      await productsApi.remove(product.id)
      setBanner(`Deleted "${product.name}".`)
      await loadProducts()
    } catch (error) {
      setBanner(error instanceof ApiError ? error.userMessage : 'Could not delete the product.')
    } finally {
      setDeletingId(null)
    }
  }

  function handleSignOut() {
    authApi.signOut()
    setIsSignedIn(false)
    setProducts([])
    setLoadState('idle')
  }

  if (!isSignedIn) {
    return (
      <main className="min-h-screen bg-slate-100 px-4 py-16">
        <header className="mb-8 text-center">
          <h1 className="text-2xl font-bold text-slate-900">Products Catalogue</h1>
        </header>

        <SignInPanel onSignIn={handleSignIn} error={signInError} isSubmitting={isSigningIn} />
      </main>
    )
  }

  return (
    <div className="min-h-screen bg-slate-100">
      <header className="border-b border-slate-200 bg-white">
        <div className="mx-auto flex max-w-6xl items-center justify-between px-4 py-4">
          <div>
            <h1 className="text-xl font-bold text-slate-900">Products Catalogue</h1>
            <p className="text-sm text-slate-500">
              Authenticated with a demo bearer token
            </p>
          </div>

          <button
            type="button"
            onClick={handleSignOut}
            className="rounded-md border border-slate-300 px-3 py-1.5 text-sm font-medium text-slate-700 transition hover:bg-slate-50 focus:outline-none focus-visible:ring-2 focus-visible:ring-slate-500"
          >
            Sign out
          </button>
        </div>
      </header>

      <main className="mx-auto max-w-6xl px-4 py-8">
        {banner && (
          <div
            role="status"
            className="mb-6 rounded-md border border-slate-200 bg-white px-4 py-3 text-sm text-slate-700 shadow-sm"
          >
            {banner}
          </div>
        )}

        <div className="grid gap-8 lg:grid-cols-[360px_1fr]">
          <section aria-labelledby="create-heading">
            <div className="rounded-xl border border-slate-200 bg-white p-6 shadow-sm">
              <h2 id="create-heading" className="mb-4 text-lg font-semibold text-slate-900">
                Add a product
              </h2>

              <ProductForm
                onSubmit={handleCreate}
                serverErrors={createErrors}
                isSubmitting={isCreating}
              />
            </div>
          </section>

          <section aria-labelledby="catalogue-heading">
            <div className="mb-4 flex flex-wrap items-center justify-between gap-3">
              <h2 id="catalogue-heading" className="text-lg font-semibold text-slate-900">
                Catalogue
                {loadState === 'loaded' && (
                  <span className="ml-2 text-sm font-normal text-slate-500">
                    {totalCount} {totalCount === 1 ? 'product' : 'products'}
                  </span>
                )}
              </h2>

              <ColourFilter
                value={colour}
                onChange={setColour}
                disabled={loadState === 'loading'}
              />
            </div>

            {/* Each state is handled explicitly, a blank screen is never a
                valid outcome. */}
            {loadState === 'loading' && (
              <StateMessage variant="loading" title="Loading products…" />
            )}

            {loadState === 'error' && (
              <StateMessage
                variant="error"
                title="Could not load products"
                detail={loadError}
                onRetry={() => void loadProducts()}
              />
            )}

            {loadState === 'loaded' && products.length === 0 && (
              <StateMessage
                variant="empty"
                title={colour ? `No ${colour.toLowerCase()} products` : 'No products yet'}
                detail={
                  colour
                    ? 'Try a different colour, or clear the filter to see everything.'
                    : 'Create one using the form to get started.'
                }
              />
            )}

            {loadState === 'loaded' && products.length > 0 && (
              <ProductTable
                products={products}
                onDelete={(product) => void handleDelete(product)}
                deletingId={deletingId}
              />
            )}
          </section>
        </div>
      </main>
    </div>
  )
}
