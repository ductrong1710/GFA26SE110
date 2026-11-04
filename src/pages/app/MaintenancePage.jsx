import { useState } from 'react'
import { Workspace, Button } from '../../components/workflow/WorkflowUi'
import MaintenanceDrawer from '../../components/workflow/MaintenanceDrawer'
import SchedulingSettings from '../../components/workflow/SchedulingSettings'
import { createVisits, dateLabel, dayOffset, deviceCount } from '../../components/workflow/maintenanceDemo'
import '../../styles/maintenance.css'

const tabs = [['upcoming', 'Upcoming maintenance'], ['overdue', 'Overdue'], ['completed', 'Completed']]
export default function MaintenancePage() {
  const [visits, setVisits] = useState(createVisits)
  const [tab, setTab] = useState('upcoming')
  const [filter, setFilter] = useState('all')
  const [search, setSearch] = useState('')
  const [selected, setSelected] = useState(null)
  const [message, setMessage] = useState('')
  const [settings, setSettings] = useState({ installation: 7, maintenance: 7 })
  const [settingsOpen, setSettingsOpen] = useState(false)
  const group = key => visits.filter(v => key === 'completed' ? v.completed : !v.completed && (key === 'overdue' ? dayOffset(v.due) < 0 : dayOffset(v.due) >= 0 && dayOffset(v.due) <= 7))
  const upcoming = group('upcoming')
  const unassigned = upcoming.filter(v => !v.assignment).length
  const rows = group(tab).filter(v => (filter === 'all' || Boolean(v.assignment) === (filter === 'assigned')) && `${v.farm} ${v.address}`.toLowerCase().includes(search.toLowerCase()))
  function save(assignment) {
    setVisits(old => old.map(v => v.id === selected.id ? { ...v, assignment } : v))
    setMessage(`Schedule confirmed for ${selected.farm}. Saved in this demo only.`)
    setSelected(null)
  }
  return <Workspace title="Scheduled maintenance" className="maintenance-workspace">
    <div className="mt-demo"><span>Interactive preview · Sample data</span><span>Changes stay in this page until you reload.</span></div>
    <div className="mt-intro"><div><h2>Plan the visit. Arrive on time.</h2><p>Fixed customer appointments · Default maintenance reservation: {settings.maintenance} days from departure.</p></div><Button onClick={() => setSettingsOpen(true)}>Scheduling settings</Button></div>
    <div className="mt-summary" aria-label="Maintenance summary">
      {[[upcoming.length, 'Farms due within 7 days', 'All rented devices at each farm', 'upcoming', 'all', ''], [unassigned, 'Awaiting assignment', 'Choose a technician and travel dates', 'upcoming', 'unassigned', 'mt-amber'], [upcoming.length - unassigned, 'Assigned visits', 'Departure and return time reserved', 'upcoming', 'assigned', ''], [group('overdue').length, 'Overdue farms', 'Review these visits first', 'overdue', 'all', 'mt-red']].map(([count, label, hint, key, choice, tone]) => <button key={label} onClick={() => { setTab(key); setFilter(choice) }}><span>{label}</span><strong className={tone}>{count}</strong><small>{hint}</small></button>)}
    </div>
    {message && <div className="mt-success" role="status">{message}<button aria-label="Dismiss confirmation" onClick={() => setMessage('')}>×</button></div>}
    <section className="mt-queue" aria-label="Maintenance visits">
      <div className="mt-tabs" role="tablist" aria-label="Visit status">{tabs.map(([key, title]) => <button key={key} role="tab" aria-selected={tab === key} onClick={() => { setTab(key); setFilter('all') }}>{title}<span>{group(key).length}</span></button>)}</div>
      <div className="mt-filters"><label className="mt-search">Search farms<input placeholder="Farm name or location" value={search} onChange={e => setSearch(e.target.value)} /></label><label>Assignment<select value={filter} onChange={e => setFilter(e.target.value)}><option value="all">All assignments</option><option value="unassigned">Unassigned</option><option value="assigned">Assigned</option></select></label><p>{rows.length} {rows.length === 1 ? 'farm' : 'farms'} · Vietnam time (UTC+7)</p></div>
      <div className="mt-table-scroll"><table className="mt-table"><thead><tr><th>Farm / location</th><th>Maintenance due</th><th>Devices</th><th>Technician</th><th>Status</th><th>Action</th></tr></thead><tbody>{rows.map(v => <tr key={v.id}>
        <td><strong>{v.farm}</strong><small>{v.address}</small><small>Contract {v.contract} · Ends {dateLabel(v.ends)}</small></td>
        <td><strong>{dateLabel(v.due)}</strong><small className={dayOffset(v.due) < 0 && !v.completed ? 'mt-red' : ''}>{v.completed ? 'Visit completed' : dayOffset(v.due) < 0 ? `${-dayOffset(v.due)} days overdue` : dayOffset(v.due) === 0 ? 'Due today' : `In ${dayOffset(v.due)} days`}</small></td>
        <td><strong>{deviceCount(v)}</strong><small>All rented devices</small></td>
        <td>{v.assignment ? <><strong>{v.assignment.technician}</strong><small>Reserved: {dateLabel(v.assignment.departure)} → {dateLabel(v.assignment.reservedUntil)}</small></> : <span className="mt-muted">Not assigned</span>}</td>
        <td><span className={`mt-status ${v.completed ? 'completed' : v.assignment ? 'assigned' : 'unassigned'}`}>{v.completed ? 'Completed' : v.assignment ? 'Assigned' : 'Unassigned'}</span></td>
        <td><Button onClick={() => setSelected(v)}>{v.completed ? 'View details' : v.assignment ? 'View / reschedule' : 'Assign technician'}</Button></td>
      </tr>)}</tbody></table>{!rows.length && <div className="mt-empty"><strong>No maintenance visits found</strong><p>Try another status or clear your search and assignment filter.</p><Button onClick={() => { setSearch(''); setFilter('all') }}>Clear filters</Button></div>}</div>
      <div className="mt-foot">Reminders start 7 days before maintenance is due. Assigned farms remain visible until the visit is completed.</div>
    </section>
    {selected && <MaintenanceDrawer visit={selected} visits={visits} defaultDays={settings.maintenance} onClose={() => setSelected(null)} onSave={save} />}
    {settingsOpen && <SchedulingSettings settings={settings} onClose={() => setSettingsOpen(false)} onSave={next => { setSettings(next); setSettingsOpen(false); setMessage('Scheduling defaults updated for new assignments. Existing assignments are unchanged. Demo only.') }} />}
  </Workspace>
}
