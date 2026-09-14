import { PRODUCT_COLOURS, type ProductColour } from '../types/product'

interface ColourFilterProps {
  value: ProductColour | null
  onChange: (colour: ProductColour | null) => void
  disabled?: boolean
}

/**
 * Colour filter for the product list.
 *
 * The empty option means "no filter" rather than a colour called "All", which
 * keeps the control's value aligned with the API: absent means every colour.
 */
export function ColourFilter({ value, onChange, disabled }: ColourFilterProps) {
  return (
    <div className="flex items-center gap-2">
      <label htmlFor="colour-filter" className="text-sm font-medium text-slate-700">
        Colour
      </label>

      <select
        id="colour-filter"
        value={value ?? ''}
        disabled={disabled}
        onChange={(event) =>
          onChange(event.target.value === '' ? null : (event.target.value as ProductColour))
        }
        className="rounded-md border border-slate-300 bg-white px-3 py-2 text-sm shadow-sm focus:border-slate-500 focus:outline-none focus:ring-1 focus:ring-slate-500 disabled:cursor-not-allowed disabled:bg-slate-100"
      >
        <option value="">All colours</option>
        {PRODUCT_COLOURS.map((colour) => (
          <option key={colour} value={colour}>
            {colour}
          </option>
        ))}
      </select>
    </div>
  )
}
