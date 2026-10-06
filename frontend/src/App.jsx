import { Navigate, Route, Routes, useParams } from 'react-router-dom'
import HomePage from './pages/HomePage'
import EquipmentPage from './pages/EquipmentPage'
import ProductDetailPage from './pages/ProductDetailPage'
import CartPage from './pages/CartPage'
import AccountPage from './pages/AccountPage'
import ProtectedRoute from './components/app/ProtectedRoute'
import RouteScroll from './components/RouteScroll'
import PublicLayout from './layouts/PublicLayout'
import AppShell from './layouts/AppShell'
import AppPlaceholderPage from './pages/app/AppPlaceholderPage'
import DashboardPage from './pages/app/DashboardPage'
import FarmsPage from './pages/app/FarmsPage'
import UsersPage from './pages/app/UsersPage'
import AlertsPage from './pages/app/AlertsPage'
import SettingsPage from './pages/app/SettingsPage'
import { ManagementProvider } from './context/ManagementProvider'
import { OperationsProvider } from './context/OperationsProvider'
import SensorsPage from './pages/app/SensorsPage'
import SensorDetailPage from './pages/app/SensorDetailPage'
import DevicesPage from './pages/app/DevicesPage'
import NotFoundPage from './pages/NotFoundPage'
import { appRoutes } from './config/appRoutes'
import './App.css'

const applicationPages = { dashboard: <DashboardPage />, farms: <FarmsPage />, users: <UsersPage />, alerts: <AlertsPage />, settings: <SettingsPage />, sensors: <SensorsPage />, 'sensors/:id': <SensorDetailPage />, devices: <DevicesPage /> }

function ProductDetailRoute() {
  const { id } = useParams()
  return <ProductDetailPage key={id} productId={id} />
}

export default function App() {
  return <>
    <RouteScroll />
    <Routes>
      <Route element={<PublicLayout />}>
        <Route path="/" element={<HomePage />} />
        <Route path="/thiet-bi" element={<EquipmentPage />} />
        <Route path="/thiet-bi/:id" element={<ProductDetailRoute />} />
        <Route path="/gio-hang" element={<CartPage />} />
        <Route path="*" element={<NotFoundPage />} />
      </Route>
      <Route path="/dang-nhap" element={<AccountPage key="login" />} />
      <Route path="/dang-ky" element={<AccountPage key="register" register />} />
      <Route path="/app" element={<ProtectedRoute />}>
        <Route element={<OperationsProvider><ManagementProvider><AppShell /></ManagementProvider></OperationsProvider>}>
          <Route index element={<Navigate to="dashboard" replace />} />
          {appRoutes.map(({ path, title, permissions }) => <Route key={path} path={path} element={
            <ProtectedRoute permissions={permissions}>{applicationPages[path] ?? <AppPlaceholderPage title={title} />}</ProtectedRoute>
          } />)}
          <Route path="*" element={<NotFoundPage application />} />
        </Route>
      </Route>
    </Routes>
  </>
}
