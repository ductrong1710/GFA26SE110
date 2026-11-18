import react from '@vitejs/plugin-react'
import basicSsl from '@vitejs/plugin-basic-ssl'
import tailwindcss from '@tailwindcss/vite'
import { defineConfig, loadEnv } from 'vite'

// https://vite.dev/config/
export default defineConfig(({ command, mode }) => {
  const env = loadEnv(mode, process.cwd(), '')
  const apiTarget = env.VITE_API_PROXY_TARGET || 'https://localhost:7043'

  return {
    plugins: [
      react(),
      tailwindcss(),
      command === 'serve' && (mode === 'local-https' || process.env.VITE_LOCAL_HTTPS === 'true') && basicSsl(),
    ],
    ...(command === 'serve' && {
      server: {
        port: 5173,
        strictPort: true,
        proxy: {
          '/api': {
            target: apiTarget,
            changeOrigin: true,
            // ASP.NET Core's local development certificate is not trusted by Node by default.
            secure: false,
          },
        },
      },
    }),
  }
})
