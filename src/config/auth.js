// Demo authentication is an explicit opt-in, including in development.
export const mockAuthEnabled = import.meta.env.VITE_ENABLE_MOCK_AUTH === 'true'
