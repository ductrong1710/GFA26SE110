import { useAuth } from '../../context/AuthContext'
import { Link } from 'react-router-dom'
import { ROLES } from '../../config/roles'
import AccessDeniedPage from './AccessDeniedPage'
import FarmOwnerDashboard from './FarmOwnerDashboard'
import AdminDashboard from './AdminDashboard'
import { PageHeader } from '../../components/app/ui'

export default function DashboardPage() {
  const { activeRole } = useAuth()
  if (activeRole === ROLES.ADMINISTRATOR) return <AdminDashboard />
  if (activeRole === ROLES.AGRICULTURAL_ENGINEER) return <PageHeader title="Agricultural workspace" description="Review new consultation requests and follow up with your assigned customers."
    actions={<Link className="app-ui-button" to="/app/consultations">Open consultations</Link>} />
  if (activeRole === ROLES.TECHNICIAN) return <PageHeader title="Technical workspace" description="Review your assigned installation appointments and work history."
    actions={<Link className="app-ui-button" to="/app/installation-jobs">Open my jobs</Link>} />
  return activeRole === ROLES.FARM_OWNER
    ? <FarmOwnerDashboard />
    : <AccessDeniedPage />
}
