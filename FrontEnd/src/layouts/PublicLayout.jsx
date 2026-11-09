import { Outlet } from 'react-router-dom'
import FloatingContactDock from '../components/FloatingContactDock'

export default function PublicLayout() {
  return <><Outlet /><FloatingContactDock /></>
}
