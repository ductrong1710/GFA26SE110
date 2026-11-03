import { Outlet } from 'react-router-dom'
import { QuickContacts } from '../components/LiveHomepage'

export default function PublicLayout() {
  return <><Outlet /><QuickContacts /></>
}
