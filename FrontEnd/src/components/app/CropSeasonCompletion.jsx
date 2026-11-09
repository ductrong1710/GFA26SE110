import { useState } from 'react'
import { api } from '../../services/api'

export default function CropSeasonCompletion({ season, onSaved, onCancel }) {
  const [pending, setPending] = useState(false)
  const [error, setError] = useState('')
  async function complete(event) {
    event.preventDefault()
    if (pending) return
    const data = new FormData(event.currentTarget)
    const amount = key => data.get(key) === '' ? null : Number(data.get(key))
    const body = { expectedRevision: season.revision, endDate: data.get('endDate') || null,
      yieldKg: amount('yieldKg'), totalCostVnd: amount('totalCostVnd'), revenueVnd: amount('revenueVnd'),
      notes: data.get('notes').trim() || null }
    setPending(true)
    setError('')
    try {
      onSaved(await api.request(`/api/crop-seasons/${season.id}/complete`, { method: 'POST', body }))
    } catch (cause) { setError(cause.message) }
    finally { setPending(false) }
  }
  return <form onSubmit={complete}><fieldset className="management-form" disabled={pending}>
    <legend>Complete {season.name}</legend>
    <p>Leave unknown results blank. Completing this season closes its cultivation log.</p>
    <label>End date<input type="date" name="endDate" min={season.startDate ?? undefined} /></label>
    <label>Yield (kg)<input type="number" name="yieldKg" min="0" step="0.0001" /></label>
    <label>Total cost (VND)<input type="number" name="totalCostVnd" min="0" step="0.01" /></label>
    <label>Revenue (VND)<input type="number" name="revenueVnd" min="0" step="0.01" /></label>
    <label>Completion notes<textarea name="notes" maxLength={4000} /></label>
    {error && <p role="alert">{error}</p>}
    <button type="submit">Confirm completion</button><button type="button" onClick={onCancel}>Cancel</button>
  </fieldset></form>
}
