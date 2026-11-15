import react from '@vitejs/plugin-react'
import basicSsl from '@vitejs/plugin-basic-ssl'
import tailwindcss from '@tailwindcss/vite'
import { defineConfig } from 'vite'

// https://vite.dev/config/
export default defineConfig(({ command, mode }) => ({
  plugins: [
    react(),
    tailwindcss(),
    command === 'serve' && (mode === 'local-https' || process.env.VITE_LOCAL_HTTPS === 'true') && basicSsl(),
  ],
}))
