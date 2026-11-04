import { useEffect, useRef, useState } from 'react'
import { Button } from './WorkflowUi'

export default function SchedulingSettings({ settings, onClose, onSave }) {
  const dialog = useRef(null)
  const [draft, setDraft] = useState(settings)
  const valid = Object.values(draft).every(v => Number.isInteger(Number(v)) && Number(v) >= 1 && Number(v) <= 60)
  useEffect(() => {
    const previous = document.activeElement
    dialog.current.showModal()
    return () => previous?.focus?.()
  }, [])
  return <dialog ref={dialog} className="mt-dialog mt-settings" aria-labelledby="mt-settings-title" onCancel={e => { e.preventDefault(); onClose() }}>
    <form className="mt-drawer" onSubmit={e => { e.preventDefault(); if (valid) onSave({ installation: Number(draft.installation), maintenance: Number(draft.maintenance) }) }}>
      <header><div><small>Scheduling policy · Demo</small><h2 id="mt-settings-title">Scheduling settings</h2></div><button type="button" className="mt-close" aria-label="Close settings" onClick={onClose}>×</button></header>
      <div className="mt-drawer-body">
        <p className="mt-help">Set the default time reserved for each trip, including outbound travel, on-site work and return travel. Counted from departure.</p>
        {['installation', 'maintenance'].map(kind => <label key={kind}>{kind === 'installation' ? 'Installation reservation (days)' : 'Maintenance reservation (days)'}<input aria-label={kind === 'installation' ? 'Installation reservation (days)' : 'Maintenance reservation (days)'} type="number" min="1" max="60" step="1" required value={draft[kind]} onChange={e => setDraft(old => ({ ...old, [kind]: e.target.value }))} /><small className="mt-help">Enter a whole number from 1 to 60 days.</small></label>)}
        <div className="mt-reserved"><strong>Existing assignments stay unchanged</strong><p>New assignments use these defaults. You can adjust the duration for each trip.</p></div>
        <div className="mt-reserved"><strong>Maintenance reminder: 7 days before due date</strong><p>This reminder window is separate from the reservation duration.</p></div>
        <p className="mt-help">Frontend preview only. Installation uses a separate default prepared here for its future assignment flow.</p>
      </div>
      <footer><Button onClick={onClose}>Cancel</Button><Button primary type="submit" disabled={!valid}>Save settings</Button></footer>
    </form>
  </dialog>
}
