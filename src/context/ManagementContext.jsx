import { createContext, useContext } from 'react'

export const ManagementContext = createContext(null)

export function useManagement() {
  const context = useContext(ManagementContext)
  if (!context) throw new Error('ManagementProvider is required.')
  return context
}
