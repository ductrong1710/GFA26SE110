import { HUMAN_ROLES, ROLE_LABELS } from '../../config/roles'
import { useAuth } from '../../context/AuthContext'
import '../../styles/app-permissions.css'

// DEMO ONLY: this previews all human roles; it does not assign roles to the user.
// Remove this override when real authentication/authorization is integrated.
export default function DemoRoleSwitcher() {
  const { activeRole, switchDemoRole, demoRoleSwitchEnabled } = useAuth()
  if (!demoRoleSwitchEnabled) return null

  return <div className="app-demo-role-switcher">
    <label htmlFor="app-demo-role">Demo only · Preview role</label>
    <select id="app-demo-role" value={activeRole ?? ''} onChange={(event) => switchDemoRole(event.target.value)}>
      {HUMAN_ROLES.map((role) => <option key={role} value={role}>{ROLE_LABELS[role]}</option>)}
    </select>
  </div>
}
