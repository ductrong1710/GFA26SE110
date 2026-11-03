import { SectionCard } from './ui'

const groups = [
  { title: 'Alert history', prefixes: ['ALERT_'], empty: 'No alerts recorded in this period.' },
  { title: 'Job progress', prefixes: ['INSTALLATION_', 'MAINTENANCE_', 'REPAIR_'], empty: 'No job activity recorded in this period.' },
  { title: 'Mission outcomes', prefixes: ['MISSION_'], empty: 'No mission outcomes recorded in this period.' },
]

export default function AdminHistory({ history }) {
  if (!history) return <p>Historical data unavailable.</p>
  return groups.map(({ title, prefixes, empty }) => {
    const rows = history.filter(row => prefixes.some(prefix => row.category.startsWith(prefix)))
    const max = Math.max(1, ...rows.map(row => row.count))
    return <SectionCard key={title} title={title} description="Recorded events over the last 30 UTC calendar days">
      {rows.length ? <ul className="admin-history">{rows.map(({ date, category, count }) => {
        const words = category.replace(/^(ALERT_|MISSION_)/, '').toLowerCase().replaceAll('_', ' ')
        const label = words[0].toUpperCase() + words.slice(1)
        return <li key={date + category}><time dateTime={date}>{date}</time> {label}
          <meter aria-label={date + ' · ' + label} value={count} max={max} /> <strong>{count}</strong>
        </li>
      })}</ul> : <p>{empty}</p>}
    </SectionCard>
  })
}
