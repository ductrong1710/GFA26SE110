import { useState } from 'react'
import { useAuth } from '../../context/AuthContext'
import { useApiQuery } from '../../services/api/useApiQuery'
import { listAll } from '../../services/api/resources'
import { ANALYTICS_DEFAULT_RANGE, parseAnalyticsTime, validateAnalyticsRange } from '../../services/api/analytics'
import { SectionCard, DataTable } from './ui'
import ApiState from './ApiState'
import { displayTime } from '../../utils/display'
import { useUiText } from '../../utils/useUiText'

export default function ChannelHistory({ channels }) {
  const { user, activeRole } = useAuth()
  const t = useUiText({
    'Channel reading history': 'Lịch sử đo của kênh',
    'Channel': 'Kênh',
    'From': 'Từ',
    'To': 'Đến',
    'Channel history': 'Lịch sử kênh',
    'Value': 'Giá trị',
    'Unit': 'Đơn vị',
    'Measured At': 'Thời điểm đo',
    'Received At': 'Thời điểm nhận',
    'Valid': 'Hợp lệ',
    'Validation Details': 'Chi tiết xác thực',
    'Yes': 'Có',
    'No': 'Không',
  })
  const [channel, setChannel] = useState('')
  const [range, setRange] = useState(ANALYTICS_DEFAULT_RANGE)
  const id = channels.some(c => c.id === Number(channel)) ? channel : channels[0]?.id
  const query = useApiQuery(`channel-history:${user.id}:${activeRole}:${id}:${range.from}:${range.to}`, async signal => {
    const error = validateAnalyticsRange(range)
    if (error) throw new Error(error)
    return listAll(`/api/sensor-channels/${id}/history`, { from: new Date(parseAnalyticsTime(range.from)).toISOString(), to: new Date(parseAnalyticsTime(range.to)).toISOString() }, signal)
  }, Boolean(id))
  return <SectionCard title={t('Channel reading history')}><label>{t('Channel')}<select value={id ?? ''} onChange={e => setChannel(e.target.value)}>{channels.map(c => <option key={c.id} value={c.id}>{c.channelCode}</option>)}</select></label><label>{t('From')}<input type="datetime-local" value={range.from} onChange={e => setRange({ ...range, from: e.target.value })} /></label><label>{t('To')}<input type="datetime-local" value={range.to} onChange={e => setRange({ ...range, to: e.target.value })} /></label><ApiState {...query} /><DataTable caption={t('Channel history')} rows={query.data ?? []} columns={[{ key: 'value', header: t('Value') }, { key: 'unit', header: t('Unit') }, { key: 'measuredAt', header: t('Measured At'), render: displayTime }, { key: 'receivedAt', header: t('Received At'), render: displayTime }, { key: 'isValid', render: v => v ? t('Yes') : t('No'), header: t('Valid') }, { key: 'validationError', header: t('Validation Details') }]} /></SectionCard>
}
