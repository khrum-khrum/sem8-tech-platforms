export function Pagination(props: {
  page: number
  totalPages: number
  disabled?: boolean
  onFirst: () => void
  onPrev: () => void
  onNext: () => void
  onLast: () => void
}) {
  const totalPages = Math.max(1, props.totalPages || 1)
  const page = Math.min(Math.max(1, props.page), totalPages)
  const disabled = Boolean(props.disabled)

  return (
    <div className="pager" role="navigation" aria-label="Пагинация">
      <button type="button" onClick={props.onFirst} disabled={disabled || page <= 1}>
        «
      </button>
      <button type="button" onClick={props.onPrev} disabled={disabled || page <= 1}>
        ‹
      </button>
      <span className="pager-label">
        Стр. <strong>{page}</strong> из <strong>{totalPages}</strong>
      </span>
      <button type="button" onClick={props.onNext} disabled={disabled || page >= totalPages}>
        ›
      </button>
      <button type="button" onClick={props.onLast} disabled={disabled || page >= totalPages}>
        »
      </button>
    </div>
  )
}

