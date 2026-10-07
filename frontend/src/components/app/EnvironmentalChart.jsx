import { ResponsiveContainer, LineChart, Line, XAxis, YAxis, CartesianGrid, Tooltip, ReferenceArea, Legend } from 'recharts'
import { EmptyState } from './ui'
import { formatMetric } from '../../data/mock/sensorAnalytics'

const colors = ['#176346', '#367ba6', '#b37b24', '#8665a6', '#a85460']
const timeLabel = (at) => new Date(at).toLocaleString('en-GB', { timeZone: 'Asia/Ho_Chi_Minh', day: '2-digit', month: 'short', hour: '2-digit', minute: '2-digit' })
function ChartTooltip({ active, payload, label, type }) {
  if (!active || !payload?.length) return null
  return <div className="analytics-tooltip"><strong>{timeLabel(label)} · Vietnam</strong>{payload.filter(({ value }) => value !== null && value !== undefined).map((item) => <p key={item.dataKey}><span style={{ color: item.color }}>{item.name}</span><b>{formatMetric(item.value, type)} {type?.unit}</b></p>)}<small>Hourly means of available sensor channels{payload[0]?.payload?.channels !== undefined ? ` · ${payload[0].payload.channels} contributing` : ''}. Coverage may vary by hour.</small></div>
}

export default function EnvironmentalChart({ series, type, threshold, zones, label }) {
  const lines = zones?.map((zone, index) => ({ key: `zone${zone.id}`, label: zone.name, color: colors[index % colors.length] })) ?? [{ key: 'value', label: type?.name ?? 'Average', color: colors[0] }]
  if (!series.some((row) => lines.some(({ key }) => row[key] !== null && row[key] !== undefined))) return <EmptyState title="No readings in this selection" description="This zone may not have this sensor type, or no synchronized readings fall within the date range." />
  return <div className="analytics-chart" aria-label={label ?? `${type?.name} historical trend`}>
    <ResponsiveContainer width="100%" height="100%" minWidth={0} initialDimension={{ width: 400, height: 280 }}>
      <LineChart data={series} accessibilityLayer margin={{ top: 16, right: 16, left: 0, bottom: 8 }}>
        <CartesianGrid stroke="#e4ebe6" strokeDasharray="3 4" vertical={false} />
        <XAxis dataKey="at" type="number" domain={['dataMin', 'dataMax']} scale="time" tickFormatter={(at) => new Date(at).toLocaleTimeString('en-GB', { timeZone: 'Asia/Ho_Chi_Minh', hour: '2-digit', minute: '2-digit' })} tick={{ fontSize: 11 }} minTickGap={40} />
        <YAxis width={52} domain={['auto', 'auto']} tick={{ fontSize: 11 }} tickFormatter={(value) => formatMetric(value, type)} />
        {threshold && <ReferenceArea y1={threshold.min} y2={threshold.max} ifOverflow="extendDomain" fill="#cce5d3" fillOpacity={0.35} strokeOpacity={0} />}
        <Tooltip content={<ChartTooltip type={type} />} wrapperStyle={{ zIndex: 5, maxWidth: '100%' }} />
        {zones && <Legend wrapperStyle={{ fontSize: 12, paddingTop: 12 }} />}
        {lines.map((line) => <Line key={line.key} type="linear" dataKey={line.key} name={line.label} stroke={line.color} strokeWidth={2} dot={{ r: 2 }} activeDot={{ r: 5 }} connectNulls={false} isAnimationActive={false} />)}
      </LineChart>
    </ResponsiveContainer>
  </div>
}
