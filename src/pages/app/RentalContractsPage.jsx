import { useState } from 'react'
import { api } from '../../services/api'
import { useApiQuery } from '../../services/api/useApiQuery'
import { useAuth } from '../../context/AuthContext'
import { PERMISSIONS as P } from '../../config/permissions'
import ApiState from '../../components/app/ApiState'
import { DataTable, PageHeader, SectionCard, StatusBadge } from '../../components/app/ui'
import { displayTime } from '../../utils/display'

export default function RentalContractsPage() {
  const { can } = useAuth()
  const [page, setPage] = useState(1)
  const [selected, setSelected] = useState(null)
  const [values, setValues] = useState({ action: '', note: '', endAt: '', devicesReturned: false })
  const [pending, setPending] = useState(false)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const query = useApiQuery(`rental-contracts:${page}`, signal => api.request('/api/rental-contracts', { query: { page }, signal }))
  function select(row) {
    setSelected(row)
    setValues({ action: row.status === 'DRAFT' ? 'activate' : 'extend', note: '', endAt: '', devicesReturned: false })
  }
  async function open(id) {
    setPending(true); setError(''); setNotice(''); setSelected(null)
    try { select(await api.request(`/api/rental-contracts/${id}`)) }
    catch (err) { setError(err.message) } finally { setPending(false) }
  }
  async function decide(event) {
    event.preventDefault(); if (pending) return
    setPending(true); setError(''); setNotice('')
    try {
      const body = { expectedRevision: selected.revision, note: values.note }
      if (values.action === 'extend') {
        const end = new Date(values.endAt)
        if (!Number.isFinite(end.getTime()) || end <= new Date(selected.endAt)) throw new Error('Choose a rental end later than the current end.')
        body.endAt = end.toISOString()
      }
      if (values.action === 'end') {
        if (!values.devicesReturned) throw new Error('Confirm physical return of all assigned devices before ending this contract.')
        body.devicesReturned = true
      }
      select(await api.request(`/api/rental-contracts/${selected.id}/${values.action}`, { method: 'POST', body }))
      query.reload(); setNotice('Rental contract updated.')
    } catch (err) { setError(err.message) } finally { setPending(false) }
  }
  const field = name => ({ value: values[name], onChange: event => setValues({ ...values, [name]: event.target.value }) })
  return <><PageHeader title="Rental contracts" description="Agreed rental periods, assigned equipment and contract history." />
    <ApiState loading={query.loading} error={query.error} />{error && <p role="alert">{error}</p>}{notice && <p role="status">{notice}</p>}
    {selected && <SectionCard title={`Rental contract #${selected.id}`} actions={<StatusBadge status={selected.status} />}>
      <dl><dt>Farm</dt><dd>#{selected.farmId}</dd><dt>Farm Owner</dt><dd>#{selected.ownerId}</dd><dt>Installation job</dt><dd>#{selected.installationJobId}</dd>
        <dt>Rental start</dt><dd>{displayTime(selected.startAt)}</dd><dt>Rental end</dt><dd>{displayTime(selected.endAt)}</dd></dl>
      <h3>Assigned devices</h3><ul>{selected.devices.map(device => <li key={device.id}><strong>{device.code} · {device.name}</strong>
        <p>{device.installationLocation}</p><p>{device.returnedAt ? `Returned ${displayTime(device.returnedAt)}` : 'Assigned to this contract'}</p></li>)}</ul>
      {can(P.RENTALS_MANAGE) && ['DRAFT', 'ACTIVE', 'EXPIRED'].includes(selected.status) && <form className="management-form" onSubmit={decide} aria-label="Manage rental contract">
        <fieldset disabled={pending} className="management-form"><legend>Manage contract</legend>
          <label>Contract action<select {...field('action')}>
            {selected.status === 'DRAFT' ? <option value="activate">Activate agreed rental</option> : <option value="extend">Record confirmed extension</option>}
            <option value="end">End after device return</option>
          </select></label>
          {values.action === 'activate' && <p>Activation is available once the agreed rental start has arrived and before the rental end.</p>}
          {values.action === 'extend' && <><p>Record the extension confirmed outside the system.</p><label>New rental end<input type="datetime-local" required {...field('endAt')} /></label></>}
          {values.action === 'end' && <label><input type="checkbox" required checked={values.devicesReturned} onChange={event => setValues({ ...values, devicesReturned: event.target.checked })} />All assigned devices have been physically returned</label>}
          <label>Decision note<textarea required maxLength={2000} {...field('note')} /></label>
          <button type="submit">Record contract action</button>
        </fieldset>
      </form>}
      <h3>Contract history</h3><ol>{selected.history.map(item => <li key={item.id}><strong>{item.action}</strong> · {displayTime(item.createdAt)} · {item.userId == null ? 'System' : `User #${item.userId}`}<p>{item.note}</p></li>)}</ol>
    </SectionCard>}
    <DataTable caption="Rental contracts" loading={query.loading} rows={query.data?.items ?? []} columns={[
      { key: 'id', header: 'Contract' }, { key: 'farmId', header: 'Farm' }, { key: 'startAt', header: 'Rental start', render: displayTime },
      { key: 'endAt', header: 'Rental end', render: displayTime }, { key: 'status', header: 'Status', render: value => <StatusBadge status={value} /> },
      { key: 'action', header: 'Action', render: (_, row) => <button disabled={pending} onClick={() => open(row.id)}>Open contract #{row.id}</button> },
    ]} footer={<div className="management-row-actions"><button disabled={page === 1 || query.loading} onClick={() => setPage(page - 1)}>Previous</button>
      <span>Page {page}</span><button disabled={query.loading || page * 20 >= (query.data?.pagination.totalItems ?? 0)} onClick={() => setPage(page + 1)}>Next</button></div>} />
  </>
}
