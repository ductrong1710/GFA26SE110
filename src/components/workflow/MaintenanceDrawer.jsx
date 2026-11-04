import { useEffect, useRef, useState } from 'react'
import { Button } from './WorkflowUi'
import { conflictFor, dateLabel, dayOffset, reservationEnd, technicians } from './maintenanceDemo'

export default function MaintenanceDrawer({ visit, visits, defaultDays = 7, onClose, onSave }) {
  const dialog = useRef(null)
  const [form, setForm] = useState({ ...(visit.assignment ?? { departure: '', technician: '', note: '' }), workStart: visit.due })
  const initialDays = visit.assignment?.reservationDays ?? defaultDays
  const [days, setDays] = useState(initialDays)
  const [adjust, setAdjust] = useState(initialDays !== (visit.assignment?.defaultDays ?? defaultDays))
  const reservedUntil = reservationEnd(form.departure, days)
  const ordered = Boolean(form.departure && reservedUntil && form.departure <= visit.due && visit.due < reservedUntil)
  const conflict = conflictFor(form.technician, form.departure, reservedUntil, visits, visit.id)
  const valid = ordered && form.technician && !conflict
  useEffect(() => {
    const previous = document.activeElement
    const bodyOverflow = document.body.style.overflow
    document.body.style.overflow = 'hidden'
    dialog.current.showModal()
    return () => { document.body.style.overflow = bodyOverflow; previous?.focus?.() }
  }, [])
  function change(key, value) { setForm(old => ({ ...old, [key]: value })) }
  return <dialog ref={dialog} className="mt-dialog" aria-labelledby="mt-drawer-title" onCancel={e => { e.preventDefault(); onClose() }} onClick={e => { if (e.target === e.currentTarget) onClose() }}>
    <form className="mt-drawer" onSubmit={e => { e.preventDefault(); if (valid && !visit.completed) onSave({ ...form, workStart: visit.due, reservedUntil, reservationDays: Number(days), defaultDays: visit.assignment?.defaultDays ?? defaultDays }) }}>
      <header><div><small>{visit.completed ? 'Completed visit' : visit.assignment ? 'Review / reschedule' : 'Assign maintenance visit'}</small><h2 id="mt-drawer-title">{visit.farm}</h2></div><button type="button" className="mt-close" aria-label="Close visit details" onClick={onClose}>×</button></header>
      <div className="mt-drawer-body">
        <section className="mt-farm-info"><p>{visit.address}</p><p>{visit.contact} · {visit.phone}</p><div><span>Maintenance due<strong className={dayOffset(visit.due) < 0 && !visit.completed ? 'mt-red' : ''}>{dateLabel(visit.due)}</strong></span><span>Rental ends<strong>{dateLabel(visit.ends)}</strong></span></div><small>Contract {visit.contract} · Maintenance every 3 months</small></section>
        <section><h3>01 <span>Devices covered</span></h3><div className="mt-devices">{visit.devices.filter(d => d.count).map(d => <span key={d.name}>{d.name}<strong>{d.count}</strong></span>)}</div></section>
        <section><h3>02 <span>Schedule · UTC+7</span></h3>
          <div className="mt-date-grid"><label>Departure<input type="datetime-local" required disabled={visit.completed} max={visit.due} value={form.departure} onChange={e => change('departure', e.target.value)} /></label><label>Scheduled arrival at farm<input type="datetime-local" readOnly value={visit.due} /></label></div>
          <div className="mt-reservation-controls"><label>Reservation duration (days)<input type="number" min="1" max="60" step="1" required readOnly={!adjust} disabled={visit.completed} value={days} onChange={e => { setDays(e.target.value); change('technician', '') }} /></label>
            {!visit.completed && <label className="mt-adjust"><input type="checkbox" checked={adjust} onChange={e => { setAdjust(e.target.checked); if (!e.target.checked) { setDays(visit.assignment?.defaultDays ?? defaultDays); change('technician', '') } }} />Adjust reservation for this trip</label>}
          </div>
          {form.departure && !ordered && <p className="mt-error" role="alert">Depart before the appointment. Keep arrival within the reserved period.</p>}
          <div className="mt-reserved"><strong>Reserved · {days} days</strong><p>{reservedUntil ? `${form.departure.replace('T', ' ')} → ${reservedUntil.replace('T', ' ')} (UTC+7)` : 'Choose departure first.'}</p></div>
          {visit.completed && <p className="mt-help">Actual work completion: {visit.assignment?.actualCompletedAt?.replace('T', ' ') ?? 'Not reported'}</p>}
        </section>
        <section><h3>03 <span>Assign a technician</span></h3>{!ordered && <p className="mt-help">Choose departure to check availability.</p>}
          <label>Technician<select required disabled={!ordered || visit.completed} value={form.technician} onChange={e => change('technician', e.target.value)}><option value="">Select a technician</option>{technicians.map(name => { const busy = conflictFor(name, form.departure, reservedUntil, visits, visit.id); return <option key={name} value={name} disabled={Boolean(busy)}>{name}{ordered ? busy ? ' — Busy' : ' — Available' : ''}</option> })}</select></label>
          {ordered && !visit.completed && <div className="mt-availability">{technicians.map(name => { const busy = conflictFor(name, form.departure, reservedUntil, visits, visit.id); return <div key={name}><span>{name}</span><small className={busy ? 'mt-amber' : ''}>{busy ? `${busy.type} · ${busy.farm} · ${dateLabel(busy.start)}–${dateLabel(busy.end)}` : `Available`}</small></div> })}</div>}
          {conflict && <p className="mt-error" role="alert">Schedule conflict. Choose another technician.</p>}
        </section>
        <label>Notes<textarea rows="2" disabled={visit.completed} placeholder="Optional" value={form.note} onChange={e => change('note', e.target.value)} /></label>
        
      </div>
      <footer><Button onClick={onClose}>{visit.completed ? 'Close' : 'Cancel'}</Button>{!visit.completed && <Button primary type="submit" disabled={!valid}>Confirm schedule</Button>}</footer>
    </form>
  </dialog>
}
