import { Link } from 'react-router-dom'
import { EmptyState, SectionCard } from './ui'

const activityNames = { WATERING: 'Watering', FERTILIZING: 'Fertilizing', PESTICIDE: 'Pesticide', INSPECTION: 'Inspection', OTHER: 'Other' }

export default function DashboardOperations({ data, admin = false }) {
  if (!data) return <p>Operational summary unavailable.</p>
  return <>
    <SectionCard title="Operational work" description="Jobs awaiting completion and approval">
      <p><Link to="/app/installation-jobs">{data.openInstallations} installation jobs</Link></p>
      <p><Link to="/app/service-jobs">{data.openMaintenance} maintenance jobs</Link></p>
      <p><Link to="/app/service-jobs">{data.openRepairs} repair jobs</Link></p>
    </SectionCard>
    <SectionCard title="Rental contracts" actions={<Link to="/app/rental-contracts">View contracts</Link>}>
      <p>{data.activeRentals} active contracts</p>
      <p>{data.expiringRentals} expiring within {data.warningDays} days</p>
      <p>{data.expiredRentals} expired contracts awaiting renewal or closure</p>
    </SectionCard>
    <SectionCard title="Recent cultivation" description={`${data.activeSeasons} active seasons`}
      actions={<Link to="/app/crop-seasons">{admin ? 'View seasons' : 'View seasons and compare'}</Link>}>
      {data.recentCultivation.length ? <ul>{data.recentCultivation.map(activity => <li key={activity.id}>
        <strong>{activity.seasonName}</strong> · {activityNames[activity.type] ?? activity.type} · <time dateTime={activity.date}>{activity.date}</time>
        <p>{activity.description}</p>
      </li>)}</ul> : <EmptyState title="No cultivation activities recorded" />}
    </SectionCard>
  </>
}
