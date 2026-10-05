// Mock auth is enabled locally; preview deployments must explicitly opt in.
export const mockAuthEnabled = import.meta.env.VITE_ENABLE_MOCK_AUTH === 'true'
  || (import.meta.env.DEV && import.meta.env.VITE_ENABLE_MOCK_AUTH !== 'false')
