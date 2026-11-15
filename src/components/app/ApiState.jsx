export default function ApiState({ loading, error, reload, vietnamese = false }) {
  if (loading) return <p role="status" className="operations-note">{vietnamese ? 'Đang tải…' : 'Loading…'}</p>
  if (error) return <div role="alert" className="management-error"><p>{error.message}</p>{reload && <button className="app-ui-button" onClick={reload}>{vietnamese ? 'Thử lại' : 'Try again'}</button>}</div>
  return null
}

