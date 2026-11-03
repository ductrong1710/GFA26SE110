import { useState } from 'react'
import { api } from '../../services/api'

export default function RentalContractForm({ jobId }) {
  const [values, setValues] = useState({ startAt: '', endAt: '', note: '' })
  const [pending, setPending] = useState(false)
  const [error, setError] = useState('')
  const [created, setCreated] = useState(null)
  async function save(event) {
    event.preventDefault(); if (pending) return
    setPending(true); setError('')
    try {
      const startAt = new Date(values.startAt), endAt = new Date(values.endAt)
      if (!Number.isFinite(startAt.getTime()) || !Number.isFinite(endAt.getTime()) || endAt <= startAt) throw new Error('Rental end must be after the agreed start.')
      setCreated(await api.request(`/api/installation-jobs/${jobId}/rental-contract`, { method: 'POST', body: { startAt: startAt.toISOString(), endAt: endAt.toISOString(), note: values.note } }))
    } catch (err) { setError(err.message) } finally { setPending(false) }
  }
  const field = name => ({ value: values[name], onChange: event => setValues({ ...values, [name]: event.target.value }) })
  if (created) return <p role="status">Rental contract #{created.id} created as a draft. <a href="/app/rental-contracts">Open rental contracts</a> to activate it when the agreed rental period begins.</p>
  return <form className="management-form" aria-label="Create rental contract" onSubmit={save}>
    <fieldset disabled={pending} className="management-form"><legend>Create rental draft</legend>
      <p>Installation approval does not activate rental. Record the agreed period; activation is a separate action.</p>
      <label>Rental start<input required type="datetime-local" {...field('startAt')} /></label>
      <label>Rental end<input required type="datetime-local" min={values.startAt || undefined} {...field('endAt')} /></label>
      <label>Agreement note<textarea required maxLength={2000} {...field('note')} /></label>
      <button type="submit">Create rental draft</button>
    </fieldset>{error && <p role="alert">{error}</p>}
  </form>
}
