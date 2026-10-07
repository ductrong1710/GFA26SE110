import { lazy, Suspense } from 'react'
import { useAuth } from '../../context/AuthContext'
import { ROLES } from '../../config/roles'
import AccessDeniedPage from './AccessDeniedPage'
import FarmOwnerDashboard from './FarmOwnerDashboard'
import AdminDashboard from './AdminDashboard'
import OperatorDashboard from './OperatorDashboard'
const EngineerDashboard = lazy(() => import('./EngineerDashboard'))

export default function DashboardPage() {
  const { activeRole } = useAuth()
  if (activeRole === ROLES.ADMINISTRATOR) return <AdminDashboard />
  if (activeRole === ROLES.UAV_DEVICE_OPERATOR) return <OperatorDashboard />
  if (activeRole === ROLES.AGRICULTURAL_ENGINEER) return <Suspense fallback={<p role="status">Loading environmental analysis…</p>}><EngineerDashboard /></Suspense>
  return activeRole === ROLES.FARM_OWNER
    ? <FarmOwnerDashboard />
    : <AccessDeniedPage />
}
