import { useAuth } from '../../context/AuthContext'
import { ROLES } from '../../config/roles'
import AppPlaceholderPage from './AppPlaceholderPage'
import FarmOwnerDashboard from './FarmOwnerDashboard'
import AdminDashboard from './AdminDashboard'

export default function DashboardPage() {
  const { activeRole } = useAuth()
  if (activeRole === ROLES.ADMINISTRATOR) return <AdminDashboard />
  return activeRole === ROLES.FARM_OWNER
    ? <FarmOwnerDashboard />
    : <AppPlaceholderPage title="Tổng quan" />
}
