import { Navigate, Outlet, useLocation } from 'react-router-dom'
import { useAuth } from '../../context/AuthContext'
import AccessDeniedPage from '../../pages/app/AccessDeniedPage'

export default function ProtectedRoute({ permissions = [], children }) {
  const { isAuthenticated, activeRole, canAny } = useAuth()
  const location = useLocation()

  if (!isAuthenticated) return <Navigate to="/dang-nhap" replace state={{ from: location }} />
  if (!activeRole || (permissions.length > 0 && !canAny(permissions))) return <AccessDeniedPage />

  return children ?? <Outlet />
}
