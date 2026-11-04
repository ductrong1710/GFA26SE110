import { parseVietnamDateTime, vietnamDateTimeInput } from '../../utils/display'
import { useState } from 'react'
import { api } from '../../services/api'
import { listAll } from '../../services/api/resources'
import { useApiQuery } from '../../services/api/useApiQuery'
import ApiState from './ApiState'

export default function ServiceJobAssignmentForm({ incidentId, job, onSaved }) {
  const reopen = job?.status === 'CLOSED'
  const [values, setValues] = useState({ type: job?.type ?? 'REPAIR', technicianId: job?.technicianId ?? '', scheduledAt: vietnamDateTimeInput(job?.scheduledAt), note: '' })
  const [pending, setPending] = useState(false)
  const [error, setError] = useState('')
  const technicians = useApiQuery('installation-technicians', signal => listAll('/api/installation-jobs/technicians', {}, signal))
  async function save(event) {
    event.preventDefault(); if (pending) return
    setPending(true); setError('')
    try {
      const row = await api.request(job ? `/api/service-jobs/${job.id}/assignment` : `/api/device-incidents/${incidentId}/jobs`, {
        method: 'POST', body: { type: values.type, expectedRevision: job?.revision ?? 0, technicianId: Number(values.technicianId), scheduledAt: parseVietnamDateTime(values.scheduledAt).toISOString(), note: values.note, ...(reopen && { reopen: true }) },
      })
      onSaved(row)
    } catch (err) { setError(err.message) } finally { setPending(false) }
  }
  const field = name => ({ value: values[name], onChange: event => setValues({ ...values, [name]: event.target.value }) })
  return <form className="management-form" onSubmit={save} aria-label="Service assignment">
    <ApiState loading={technicians.loading} error={technicians.error} />
    <fieldset disabled={pending} className="management-form"><legend>{reopen ? 'Reopen service work' : job ? 'Reassign or reschedule' : 'Schedule service'}</legend>
      <label>Service type<select required disabled={Boolean(job)} {...field('type')}><option value="REPAIR">Repair</option><option value="MAINTENANCE">Maintenance</option></select></label>
      <label>Technician<select required {...field('technicianId')}><option value="">Choose active technician</option>
        {(technicians.data ?? []).map(row => <option key={row.id} value={row.id}>{row.fullName}</option>)}</select></label>
      <label>Appointment<input type="datetime-local" required {...field('scheduledAt')} /></label>
      <label>Assignment note<textarea required maxLength={2000} {...field('note')} /></label>
      <button type="submit" className="app-ui-button" disabled={technicians.loading || !!technicians.error}>{reopen ? 'Reopen service work' : job ? 'Save assignment' : 'Create service job'}</button>
    </fieldset>{error && <p role="alert">{error}</p>}
  </form>
}
