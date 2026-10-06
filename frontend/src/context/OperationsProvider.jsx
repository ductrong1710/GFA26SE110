import { useState } from 'react'
import { useAuth } from './AuthContext'
import { OperationsContext } from './OperationsContext'
import { createOperationsState, changeOperationsState } from '../data/mock/operationsState'

export function OperationsProvider({ children }) {
  const { user, activeRole } = useAuth()
  const [operations, setOperations] = useState(createOperationsState)
  const changeOperations = (change, workspace) => {
    const next = changeOperationsState(operations, change, workspace, { id: user.id, activeRole })
    setOperations(next)
    return next
  }
  return <OperationsContext.Provider value={{ operations, changeOperations }}>{children}</OperationsContext.Provider>
}
