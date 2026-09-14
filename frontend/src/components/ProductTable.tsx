import type { Product } from '../types/product'

interface ProductTableProps {
  products: Product[]
  onDelete?: (product: Product) => void
  deletingId?: string | null
}

/** Maps a product colour to a swatch, so the colour is visible, not just named. */
const COLOUR_SWATCH: Record<string, string> = {
  Red: 'bg-red-500',
  Green: 'bg-green-500',
  Blue: 'bg-blue-500',
  Yellow: 'bg-yellow-400',
  Orange: 'bg-orange-500',
  Purple: 'bg-purple-500',
  Pink: 'bg-pink-400',
  Brown: 'bg-amber-700',
  Black: 'bg-slate-900',
  White: 'bg-white border border-slate-300',
  Grey: 'bg-slate-400',
  Silver: 'bg-slate-300',
  Gold: 'bg-amber-400',
}

function formatPrice(price: number, currency: string): string {
  try {
    return new Intl.NumberFormat('en-GB', { style: 'currency', currency }).format(price)
  } catch {
    // An unrecognised currency code must not blank the whole table.
    return `${price.toFixed(2)} ${currency}`
  }
}

export function ProductTable({ products, onDelete, deletingId }: ProductTableProps) {
  return (
    // Horizontal scroll rather than a squashed table on narrow screens.
    <div className="overflow-x-auto rounded-lg border border-slate-200">
      <table className="min-w-full divide-y divide-slate-200 text-sm">
        <caption className="sr-only">Products in the catalogue</caption>

        <thead className="bg-slate-50">
          <tr>
            <Th>Name</Th>
            <Th>SKU</Th>
            <Th>Colour</Th>
            <Th className="text-right">Price</Th>
            <Th>Created</Th>
            {onDelete && <Th className="text-right">Actions</Th>}
          </tr>
        </thead>

        <tbody className="divide-y divide-slate-100 bg-white">
          {products.map((product) => (
            <tr key={product.id} className="hover:bg-slate-50">
              <td className="px-4 py-3">
                <div className="font-medium text-slate-900">{product.name}</div>
                {product.description && (
                  <div className="mt-0.5 max-w-md truncate text-xs text-slate-500">
                    {product.description}
                  </div>
                )}
              </td>

              <td className="px-4 py-3 font-mono text-xs text-slate-600">{product.sku}</td>

              <td className="px-4 py-3">
                <span className="inline-flex items-center gap-2">
                  <span
                    className={`size-3 shrink-0 rounded-full ${
                      COLOUR_SWATCH[product.colour] ?? 'bg-slate-300'
                    }`}
                    aria-hidden="true"
                  />
                  {product.colour}
                </span>
              </td>

              <td className="px-4 py-3 text-right tabular-nums text-slate-900">
                {formatPrice(product.price, product.currency)}
              </td>

              <td className="px-4 py-3 text-xs text-slate-500">
                <time dateTime={product.createdAt}>
                  {new Date(product.createdAt).toLocaleDateString('en-GB')}
                </time>
              </td>

              {onDelete && (
                <td className="px-4 py-3 text-right">
                  <button
                    type="button"
                    onClick={() => onDelete(product)}
                    disabled={deletingId === product.id}
                    // The visible label is just "Delete"; the accessible name
                    // says which product, so it is unambiguous out of context.
                    aria-label={`Delete ${product.name}`}
                    className="rounded px-2 py-1 text-xs font-medium text-red-600 transition hover:bg-red-50 focus:outline-none focus-visible:ring-2 focus-visible:ring-red-500 disabled:opacity-50"
                  >
                    {deletingId === product.id ? 'Deleting…' : 'Delete'}
                  </button>
                </td>
              )}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}

function Th({ children, className = '' }: { children: React.ReactNode; className?: string }) {
  return (
    <th
      scope="col"
      className={`px-4 py-3 text-left text-xs font-semibold uppercase tracking-wide text-slate-600 ${className}`}
    >
      {children}
    </th>
  )
}
