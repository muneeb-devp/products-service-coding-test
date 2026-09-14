import { useState, type FormEvent } from 'react'
import { PRODUCT_COLOURS, type CreateProductRequest, type ProductColour } from '../types/product'

interface ProductFormProps {
  onSubmit: (product: CreateProductRequest) => Promise<void>
  /** Field errors returned by the API, keyed by camelCased field name. */
  serverErrors?: Record<string, string>
  isSubmitting?: boolean
}

interface FormState {
  name: string
  description: string
  colour: ProductColour
  price: string
  sku: string
}

const EMPTY_FORM: FormState = {
  name: '',
  description: '',
  colour: 'Red',
  price: '',
  sku: '',
}

/**
 * Client-side validation mirroring the API's rules.
 *
 * The point is feedback, not security: the server validates independently and
 * is the only thing that can be trusted. Checking here saves a round trip and
 * lets the user fix a typo without waiting for the network.
 *
 * The limits are kept deliberately identical to the server's, so the form never
 * accepts something the API will reject.
 */
function validate(form: FormState): Record<string, string> {
  const errors: Record<string, string> = {}

  if (!form.name.trim()) {
    errors.name = 'Product name is required.'
  } else if (form.name.trim().length > 200) {
    errors.name = 'Product name must be 200 characters or fewer.'
  }

  if (form.description.trim().length > 2000) {
    errors.description = 'Description must be 2000 characters or fewer.'
  }

  if (!form.price.trim()) {
    errors.price = 'Price is required.'
  } else {
    const price = Number(form.price)

    if (Number.isNaN(price)) {
      errors.price = 'Price must be a number.'
    } else if (price < 0) {
      errors.price = 'Price must be zero or greater.'
    } else if (!/^\d+(\.\d{1,2})?$/.test(form.price.trim())) {
      errors.price = 'Price must have no more than two decimal places.'
    }
  }

  const sku = form.sku.trim()

  if (!sku) {
    errors.sku = 'SKU is required.'
  } else if (sku.length < 3 || sku.length > 32) {
    errors.sku = 'SKU must be between 3 and 32 characters.'
  } else if (!/^[A-Za-z0-9-]+$/.test(sku)) {
    errors.sku = 'SKU may only contain letters, digits and hyphens.'
  }

  return errors
}

export function ProductForm({ onSubmit, serverErrors = {}, isSubmitting }: ProductFormProps) {
  const [form, setForm] = useState<FormState>(EMPTY_FORM)
  const [clientErrors, setClientErrors] = useState<Record<string, string>>({})
  const [hasSubmitted, setHasSubmitted] = useState(false)

  // Client-side errors take precedence once the user has tried to submit;
  // server errors fill in anything only the API can know, such as a duplicate
  // SKU.
  const errors = { ...serverErrors, ...clientErrors }

  function update<K extends keyof FormState>(field: K, value: FormState[K]) {
    setForm((current) => ({ ...current, [field]: value }))

    // Re-validate as the user types, but only after a failed submit. Showing
    // "name is required" before they have typed anything is noise.
    if (hasSubmitted) {
      setClientErrors(validate({ ...form, [field]: value }))
    }
  }

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    setHasSubmitted(true)

    const validationErrors = validate(form)
    setClientErrors(validationErrors)

    if (Object.keys(validationErrors).length > 0) {
      return
    }

    try {
      await onSubmit({
        name: form.name.trim(),
        description: form.description.trim() || null,
        colour: form.colour,
        price: Number(form.price),
        sku: form.sku.trim(),
      })
    } catch {
      // The parent surfaces the failure (a field error or a banner); this only
      // decides what happens to the form. Keep everything the user typed so
      // they can correct and retry.
      //
      // Caught rather than allowed to propagate: a form's onSubmit handler is
      // fire-and-forget as far as React is concerned, so an escaping rejection
      // becomes an unhandled promise rejection in the console and in whatever
      // error reporting the page has wired up.
      return
    }

    // Only reset once the call has actually succeeded.
    setForm(EMPTY_FORM)
    setClientErrors({})
    setHasSubmitted(false)
  }

  function fieldError(field: string) {
    return errors[field]
  }

  return (
    <form onSubmit={handleSubmit} noValidate className="space-y-4" aria-label="Create product">
      <Field id="name" label="Name" error={fieldError('name')} required>
        <input
          id="name"
          type="text"
          value={form.name}
          onChange={(e) => update('name', e.target.value)}
          aria-invalid={Boolean(fieldError('name'))}
          aria-describedby={fieldError('name') ? 'name-error' : undefined}
          className={inputClass(Boolean(fieldError('name')))}
          placeholder="Ergonomic Desk Lamp"
        />
      </Field>

      <Field id="sku" label="SKU" error={fieldError('sku')} required>
        <input
          id="sku"
          type="text"
          value={form.sku}
          onChange={(e) => update('sku', e.target.value)}
          aria-invalid={Boolean(fieldError('sku'))}
          aria-describedby={fieldError('sku') ? 'sku-error' : undefined}
          className={inputClass(Boolean(fieldError('sku')))}
          placeholder="LAMP-001"
        />
      </Field>

      <div className="grid gap-4 sm:grid-cols-2">
        <Field id="colour" label="Colour" error={fieldError('colour')} required>
          <select
            id="colour"
            value={form.colour}
            onChange={(e) => update('colour', e.target.value as ProductColour)}
            className={inputClass(Boolean(fieldError('colour')))}
          >
            {PRODUCT_COLOURS.map((colour) => (
              <option key={colour} value={colour}>
                {colour}
              </option>
            ))}
          </select>
        </Field>

        <Field id="price" label="Price (GBP)" error={fieldError('price')} required>
          <input
            id="price"
            type="text"
            inputMode="decimal"
            value={form.price}
            onChange={(e) => update('price', e.target.value)}
            aria-invalid={Boolean(fieldError('price'))}
            aria-describedby={fieldError('price') ? 'price-error' : undefined}
            className={inputClass(Boolean(fieldError('price')))}
            placeholder="49.99"
          />
        </Field>
      </div>

      <Field id="description" label="Description" error={fieldError('description')}>
        <textarea
          id="description"
          rows={3}
          value={form.description}
          onChange={(e) => update('description', e.target.value)}
          aria-invalid={Boolean(fieldError('description'))}
          aria-describedby={fieldError('description') ? 'description-error' : undefined}
          className={inputClass(Boolean(fieldError('description')))}
          placeholder="Optional"
        />
      </Field>

      <button
        type="submit"
        disabled={isSubmitting}
        className="w-full rounded-md bg-slate-900 px-4 py-2.5 text-sm font-semibold text-white transition hover:bg-slate-700 focus:outline-none focus-visible:ring-2 focus-visible:ring-slate-500 focus-visible:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-60"
      >
        {isSubmitting ? 'Creating…' : 'Create product'}
      </button>
    </form>
  )
}

function inputClass(hasError: boolean) {
  const base =
    'w-full rounded-md border px-3 py-2 text-sm shadow-sm focus:outline-none focus:ring-1'
  return hasError
    ? `${base} border-red-400 focus:border-red-500 focus:ring-red-500`
    : `${base} border-slate-300 focus:border-slate-500 focus:ring-slate-500`
}

interface FieldProps {
  id: string
  label: string
  error?: string
  required?: boolean
  children: React.ReactNode
}

function Field({ id, label, error, required, children }: FieldProps) {
  return (
    <div>
      <label htmlFor={id} className="mb-1 block text-sm font-medium text-slate-700">
        {label}
        {required && (
          <span className="ml-0.5 text-red-600" aria-hidden="true">
            *
          </span>
        )}
      </label>

      {children}

      {/* Tied to the input by aria-describedby, so a screen reader announces
          the message when focus lands on the offending field. */}
      {error && (
        <p id={`${id}-error`} role="alert" className="mt-1 text-sm text-red-600">
          {error}
        </p>
      )}
    </div>
  )
}
