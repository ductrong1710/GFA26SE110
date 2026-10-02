import HomePage from './pages/HomePage'
import EquipmentPage from './pages/EquipmentPage'
import ProductDetailPage from './pages/ProductDetailPage'
import CartPage from './pages/CartPage'
import AccountPage from './pages/AccountPage'
import FloatingContactDock from './components/FloatingContactDock'
import './App.css'

export default function App() {
  const path = window.location.pathname.replace(/\/$/, '')
  const productDetailMatch = path.match(/^\/thiet-bi\/([^/]+)$/)
  const isAccountPage = path === '/dang-nhap' || path === '/dang-ky'
  const page = productDetailMatch ? <ProductDetailPage productId={productDetailMatch[1]} /> : path === '/thiet-bi' ? <EquipmentPage /> : path === '/gio-hang' ? <CartPage /> : path === '/dang-nhap' ? <AccountPage /> : path === '/dang-ky' ? <AccountPage register /> : <HomePage />
  return <>{page}{!isAccountPage && <FloatingContactDock />}</>
}
