import { useState } from 'react'
import { useAuth } from '../../context/AuthContext'
import { useApiQuery } from '../../services/api/useApiQuery'
import { listAll } from '../../services/api/resources'
import { ANALYTICS_DEFAULT_RANGE, parseAnalyticsTime, validateAnalyticsRange } from '../../services/api/analytics'
import { SectionCard, DataTable } from './ui'
import ApiState from './ApiState'
import { displayTime } from '../../utils/display'

export default function ChannelHistory({ channels }) {
  const { user, activeRole } = useAuth()
  const [channel, setChannel] = useState('')
  const [range, setRange] = useState(ANALYTICS_DEFAULT_RANGE)
  const id = channels.some(c => c.id === Number(channel)) ? channel : channels[0]?.id
  const query = useApiQuery(`channel-history:${user.id}:${activeRole}:${id}:${range.from}:${range.to}`, async signal => {
    const error = validateAnalyticsRange(range)
    if (error) throw new Error(error)
    return listAll(`/api/sensor-channels/${id}/history`, { from: new Date(parseAnalyticsTime(range.from)).toISOString(), to: new Date(parseAnalyticsTime(range.to)).toISOString() }, signal)
  }, Boolean(id) && !user.isMock)
  if (user.isMock) return null
  return <SectionCard title="Channel reading history"><label>Channel<select value={id ?? ''} onChange={e => setChannel(e.target.value)}>{channels.map(c => <option key={c.id} value={c.id}>{c.channelCode}</option>)}</select></label><label>From<input type="datetime-local" value={range.from} onChange={e => setRange({ ...range, from: e.target.value })} /></label><label>To<input type="datetime-local" value={range.to} onChange={e => setRange({ ...range, to: e.target.value })} /></label><ApiState {...query} /><DataTable caption="Channel history" rows={query.data ?? []} columns={[{ key: 'value', header: 'Value' }, { key: 'unit', header: 'Unit' }, { key: 'measuredAt', header: 'Measured At', render: displayTime }, { key: 'receivedAt', header: 'Received At', render: displayTime }, { key: 'isValid', header: 'Valid', render: v => v ? 'Yes' : 'No' }, { key: 'validationError', header: 'Validation Details' }]} /></SectionCard>
}
