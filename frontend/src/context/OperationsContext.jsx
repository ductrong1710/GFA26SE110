import { createContext, useContext } from 'react'
export const OperationsContext = createContext(null)
export function useOperations() {
  const value = useContext(OperationsContext)
  if (!value) throw new Error('OperationsProvider is required.')
  return value
}
