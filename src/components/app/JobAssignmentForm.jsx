import { useState } from 'react'
import { api } from '../../services/api'
import { listAll } from '../../services/api/resources'
import { useApiQuery } from '../../services/api/useApiQuery'
import ApiState from './ApiState'

function localTime(value) {
  if (!value) return ''
  const date = new Date(value)
  return new Date(date.getTime() - date.getTimezoneOffset() * 60000).toISOString().slice(0, 16)
}

export default function JobAssignmentForm({ requestId, job, onSaved }) {
  const [values, setValues] = useState({ technicianId: job?.technicianId ?? '', scheduledAt: localTime(job?.scheduledAt), note: '' })
  const [pending, setPending] = useState(false)
  const [error, setError] = useState('')
  const technicians = useApiQuery('installation-technicians', signal => listAll('/api/installation-jobs/technicians', {}, signal))
  async function save(event) {
    event.preventDefault(); if (pending) return
    setPending(true); setError('')
    try {
      const row = await api.request(job ? `/api/installation-jobs/${job.id}/assignment` : `/api/installation-requests/${requestId}/jobs`, {
        method: 'POST', body: { expectedRevision: job?.revision ?? 0, technicianId: Number(values.technicianId), scheduledAt: new Date(values.scheduledAt).toISOString(), note: values.note },
      })
      onSaved(row)
    } catch (err) { setError(err.message) } finally { setPending(false) }
  }
  const field = name => ({ value: values[name], onChange: event => setValues({ ...values, [name]: event.target.value }) })
  return <form className="management-form" onSubmit={save} aria-label="Installation assignment">
    <ApiState loading={technicians.loading} error={technicians.error} />
    <fieldset disabled={pending} className="management-form"><legend>{job ? 'Reassign or reschedule' : 'Schedule installation'}</legend>
      <label>Technician<select required {...field('technicianId')}><option value="">Choose active technician</option>
        {(technicians.data ?? []).map(row => <option key={row.id} value={row.id}>{row.fullName}</option>)}</select></label>
      <label>Appointment<input type="datetime-local" required {...field('scheduledAt')} /></label>
      <label>Assignment note<textarea required maxLength={2000} {...field('note')} /></label>
      <button type="submit" className="app-ui-button" disabled={technicians.loading || !!technicians.error}>{job ? 'Save assignment' : 'Create installation job'}</button>
    </fieldset>{error && <p role="alert">{error}</p>}
  </form>
}
