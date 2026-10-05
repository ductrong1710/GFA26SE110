import { Link } from 'react-router-dom'
import { useAuth } from '../../context/AuthContext'

export default function AccessDeniedPage() {
  const { activeRole, activeRoleLabel } = useAuth()

  return <section className="app-placeholder app-access-denied" aria-labelledby="access-denied-heading">
    <h1 id="access-denied-heading">Access Denied</h1>
    <p>Your active role ({activeRoleLabel}) does not have access to this page.</p>
    <p>{activeRole ? <Link className="app-access-denied-link" to="/app/dashboard">Return to Dashboard →</Link> : <Link className="app-access-denied-link" to="/dang-nhap">Return to sign in →</Link>}</p>
  </section>
}
