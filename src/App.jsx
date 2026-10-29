import { lazy, Suspense } from 'react'
import { Navigate, Route, Routes } from 'react-router-dom'
import HomePage from './pages/HomePage'
import AccountPage from './pages/AccountPage'
import ForgotPasswordPage from './pages/ForgotPasswordPage'
import ProtectedRoute from './components/app/ProtectedRoute'
import RouteScroll from './components/RouteScroll'
import PublicLayout from './layouts/PublicLayout'
import AppShell from './layouts/AppShell'
import DashboardPage from './pages/app/DashboardPage'
import UsersPage from './pages/app/UsersPage'
import AlertsPage from './pages/app/AlertsPage'
import ProfilePage from './pages/app/ProfilePage'
import { ManagementProvider } from './context/ManagementProvider'
import { OperationsProvider } from './context/OperationsProvider'
import SensorDetailPage from './pages/app/SensorDetailPage'
import MissionsPage from './pages/app/MissionsPage'
import CreateMissionPage from './pages/app/CreateMissionPage'
import NotFoundPage from './pages/NotFoundPage'
import { appRoutes } from './config/appRoutes'
import { useUiText } from './utils/useUiText'
import './App.css'

const MissionDetailPage = lazy(() => import('./pages/app/MissionDetailPage'))
const FarmsPage = lazy(() => import('./pages/app/FarmsPage'))
const SensorsPage = lazy(() => import('./pages/app/SensorsPage'))
const DevicesPage = lazy(() => import('./pages/app/DevicesPage'))
const SensorDataPage = lazy(() => import('./pages/app/SensorDataPage'))
const ReportsPage = lazy(() => import('./pages/app/ReportsPage'))
const SyncPage = lazy(() => import('./pages/app/SyncPage'))
const SettingsPage = lazy(() => import('./pages/app/SettingsPage'))

const applicationPages = { dashboard: <DashboardPage />, farms: <FarmsPage />, users: <UsersPage />, profile: <ProfilePage />, alerts: <AlertsPage />, settings: <SettingsPage />, sensors: <SensorsPage />, 'sensors/:id': <SensorDetailPage />, devices: <DevicesPage />, missions: <MissionsPage />, 'missions/create': <CreateMissionPage />, 'missions/:id': <MissionDetailPage />, sync: <SyncPage />, 'sensor-data': <SensorDataPage />, 'sensor-data/compare-zones': <SensorDataPage compareOnly />, reports: <ReportsPage /> }

function LoadingPage() {
  const t = useUiText({ 'Loading page…': 'Đang tải trang…' })
  return <p role="status">{t('Loading page…')}</p>
}

export default function App() {
  return <>
    <RouteScroll />
    <Routes>
      <Route element={<PublicLayout />}>
        <Route path="/" element={<HomePage />} />
        <Route path="*" element={<NotFoundPage />} />
      </Route>
      <Route path="/dang-nhap" element={<AccountPage key="login" />} />
      <Route path="/quen-mat-khau" element={<ForgotPasswordPage />} />
      <Route path="/dang-ky" element={<Navigate to="/dang-nhap" replace />} />
      <Route path="/app" element={<ProtectedRoute />}>
        <Route element={<OperationsProvider><ManagementProvider><AppShell /></ManagementProvider></OperationsProvider>}>
          <Route index element={<Navigate to="dashboard" replace />} />
          {appRoutes.map(({ path, permissions }) => <Route key={path} path={path} element={
            <ProtectedRoute permissions={permissions}><Suspense fallback={<LoadingPage />}>{applicationPages[path] ?? <NotFoundPage application />}</Suspense></ProtectedRoute>
          } />)}
          <Route path="*" element={<NotFoundPage application />} />
        </Route>
      </Route>
    </Routes>
  </>
}
