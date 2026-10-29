import { lazy, Suspense } from 'react'
import { useAuth } from '../../context/AuthContext'
import { ROLES } from '../../config/roles'
import AccessDeniedPage from './AccessDeniedPage'
import FarmOwnerDashboard from './FarmOwnerDashboard'
import AdminDashboard from './AdminDashboard'
import { useUiText } from '../../utils/useUiText'
const EngineerDashboard = lazy(() => import('./EngineerDashboard'))

export default function DashboardPage() {
  const { activeRole } = useAuth()
  const t = useUiText({ 'Loading environmental analysis…': 'Đang tải phân tích môi trường…' })
  if (activeRole === ROLES.ADMINISTRATOR) return <AdminDashboard />
  if (activeRole === ROLES.AGRICULTURAL_ENGINEER) return <Suspense fallback={<p role="status">{t('Loading environmental analysis…')}</p>}><EngineerDashboard /></Suspense>
  return activeRole === ROLES.FARM_OWNER
    ? <FarmOwnerDashboard />
    : <AccessDeniedPage />
}
