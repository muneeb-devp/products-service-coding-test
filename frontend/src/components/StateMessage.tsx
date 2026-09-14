interface StateMessageProps {
  variant: 'loading' | 'empty' | 'error'
  title: string
  detail?: string
  onRetry?: () => void
}

/**
 * The loading, empty and error states of a data region.
 *
 * These three are grouped into one component on purpose: they are the states
 * most often left out, and giving them a single home makes it obvious when one
 * is missing. A blank screen on failure is a bug, not a neutral outcome.
 */
export function StateMessage({ variant, title, detail, onRetry }: StateMessageProps) {
  const tone =
    variant === 'error'
      ? 'border-red-200 bg-red-50 text-red-900'
      : 'border-slate-200 bg-slate-50 text-slate-700'

  return (
    <div
      className={`flex flex-col items-center gap-3 rounded-lg border px-6 py-12 text-center ${tone}`}
      // Announced to screen readers. An error nobody is told about is not
      // handled, it is hidden.
      role={variant === 'error' ? 'alert' : 'status'}
      aria-live={variant === 'error' ? 'assertive' : 'polite'}
      data-testid={`state-${variant}`}
    >
      {variant === 'loading' && (
        <span
          className="size-6 animate-spin rounded-full border-2 border-slate-300 border-t-slate-600"
          aria-hidden="true"
        />
      )}

      <p className="font-medium">{title}</p>
      {detail && <p className="max-w-prose text-sm opacity-80">{detail}</p>}

      {onRetry && (
        <button
          type="button"
          onClick={onRetry}
          className="rounded-md bg-slate-900 px-4 py-2 text-sm font-medium text-white transition hover:bg-slate-700 focus:outline-none focus-visible:ring-2 focus-visible:ring-slate-500 focus-visible:ring-offset-2"
        >
          Try again
        </button>
      )}
    </div>
  )
}
