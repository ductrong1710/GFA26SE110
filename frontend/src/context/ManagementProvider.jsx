import { useState } from 'react'
import { useAuth } from './AuthContext'
import { ManagementContext } from './ManagementContext'
import { createManagementState, applyManagementChange } from '../data/mock/managementState'

// Local demo data only. Directory edits do not replace the mock sign-in fixtures.
export function ManagementProvider({ children }) {
  const { user, activeRole } = useAuth()
  const [management, setManagement] = useState(createManagementState)
  const changeManagement = (change) => {
    const next = applyManagementChange(management, change, { id: user.id, activeRole })
    setManagement(next)
    return next
  }
  return <ManagementContext.Provider value={{ management, changeManagement }}>{children}</ManagementContext.Provider>
}
