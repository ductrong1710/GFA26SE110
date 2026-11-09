export default function ApiState({ loading, error, reload }) {
  if (loading) return <p role="status" className="operations-note">Loading…</p>
  if (error) return <div role="alert" className="management-error"><p>{error.message}</p>{reload && <button className="app-ui-button" onClick={reload}>Try again</button>}</div>
  return null
}

