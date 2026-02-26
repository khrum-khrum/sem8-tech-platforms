interface ErrorPanelProps {
  error: string
}

export function ErrorPanel({ error }: ErrorPanelProps) {
  if (!error) {
    return null
  }

  return (
    <div className="panel error">
      <p>{error}</p>
    </div>
  )
}
