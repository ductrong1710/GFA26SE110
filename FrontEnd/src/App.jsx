import MaintenanceSchedulesPage from './pages/app/MaintenanceSchedulesPage'
import PasswordPage from './pages/PasswordPage'
import CropSeasonsPage from './pages/app/CropSeasonsPage'
import CropSupportPage from './pages/app/CropSupportPage'
import ServiceJobsPage from './pages/app/ServiceJobsPage'
import { lazy, Suspense } from 'react'
import { Navigate, Route, Routes } from 'react-router-dom'
import HomePage from './pages/HomePage'
import AccountPage from './pages/AccountPage'
import ConsultationPage from './pages/ConsultationPage'
import ConsultationsPage, { ConsultationDetailPage } from './pages/app/ConsultationsPage'
import InstallationRequestsPage from './pages/app/InstallationRequestsPage'
import InstallationJobsPage from './pages/app/InstallationJobsPage'
import RentalContractsPage from './pages/app/RentalContractsPage'
import DeviceIncidentsPage from './pages/app/DeviceIncidentsPage'
import ProtectedRoute from './components/app/ProtectedRoute'
import RouteScroll from './components/RouteScroll'
import PublicLayout from './layouts/PublicLayout'
import AppShell from './layouts/AppShell'
import DashboardPage from './pages/app/DashboardPage'
import FarmsPage from './pages/app/FarmsPage'
import UsersPage from './pages/app/UsersPage'
import AlertsPage from './pages/app/AlertsPage'
import { ManagementProvider } from './context/ManagementProvider'
import { OperationsProvider } from './context/OperationsProvider'
import SensorsPage from './pages/app/SensorsPage'
import SensorDetailPage from './pages/app/SensorDetailPage'
import DevicesPage from './pages/app/DevicesPage'
import MissionsPage from './pages/app/MissionsPage'
import CreateMissionPage from './pages/app/CreateMissionPage'
import NotFoundPage from './pages/NotFoundPage'
import { appRoutes } from './config/appRoutes'
import './App.css'

const MissionDetailPage = lazy(() => import('./pages/app/MissionDetailPage'))
const SensorDataPage = lazy(() => import('./pages/app/SensorDataPage'))
const ReportsPage = lazy(() => import('./pages/app/ReportsPage'))
const SyncPage = lazy(() => import('./pages/app/SyncPage'))
const SettingsPage = lazy(() => import('./pages/app/SettingsPage'))

const applicationPages = { 'crop-seasons': <CropSeasonsPage />, 'crop-support': <CropSupportPage />, 'maintenance-schedules': <MaintenanceSchedulesPage />, 'service-jobs': <ServiceJobsPage />, 'device-incidents': <DeviceIncidentsPage />, 'rental-contracts': <RentalContractsPage />, 'installation-jobs': <InstallationJobsPage />, 'installation-requests': <InstallationRequestsPage />, consultations: <ConsultationsPage />, 'consultations/:id': <ConsultationDetailPage />, dashboard: <DashboardPage />, farms: <FarmsPage />, users: <UsersPage />, alerts: <AlertsPage />, settings: <SettingsPage />, sensors: <SensorsPage />, 'sensors/:id': <SensorDetailPage />, devices: <DevicesPage />, missions: <MissionsPage />, 'missions/create': <CreateMissionPage />, 'missions/:id': <MissionDetailPage />, sync: <SyncPage />, 'sensor-data': <SensorDataPage />, 'sensor-data/compare-zones': <SensorDataPage compareOnly />, reports: <ReportsPage /> }

export default function App() {
  return <>
    <RouteScroll />
    <Routes>
      <Route element={<PublicLayout />}>
        <Route path="/" element={<HomePage />} />
        <Route path="/tu-van" element={<ConsultationPage />} />
        <Route path="*" element={<NotFoundPage />} />
      </Route>
      <Route path="/quen-mat-khau" element={<PasswordPage key="forgot" mode="forgot" />} />
      <Route path="/dat-lai-mat-khau" element={<PasswordPage key="reset" mode="reset" />} />
      <Route path="/doi-mat-khau" element={<ProtectedRoute><PasswordPage key="change" mode="change" /></ProtectedRoute>} />
      <Route path="/dang-nhap" element={<AccountPage key="login" />} />
      <Route path="/dang-ky" element={<AccountPage key="register" register />} />
      <Route path="/app" element={<ProtectedRoute />}>
        <Route element={<OperationsProvider><ManagementProvider><AppShell /></ManagementProvider></OperationsProvider>}>
          <Route index element={<Navigate to="dashboard" replace />} />
          {appRoutes.map(({ path, permissions }) => <Route key={path} path={path} element={
            <ProtectedRoute permissions={permissions}><Suspense fallback={<p role="status">Loading page…</p>}>{applicationPages[path] ?? <NotFoundPage application />}</Suspense></ProtectedRoute>
          } />)}
          <Route path="*" element={<NotFoundPage application />} />
        </Route>
      </Route>
    </Routes>
  </>
}
