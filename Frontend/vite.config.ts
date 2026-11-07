import { defineConfig, loadEnv } from 'vite';
import react from '@vitejs/plugin-react';

// Development: the browser calls /api on the Vite server, which proxies to the ASP.NET Core API.
// This avoids CORS and works with the API's self-signed development certificate.
// Production: set VITE_API_BASE_URL to the deployed API origin at build time.
export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '');
  const proxyTarget = env.VITE_API_PROXY_TARGET || 'https://localhost:57314';

  return {
    plugins: [react()],
    server: {
      port: 5173,
      strictPort: true,
      proxy: {
        '/api': {
          target: proxyTarget,
          changeOrigin: true,
          // The ASP.NET Core dev certificate is self-signed; only relevant to the local dev proxy.
          secure: false,
        },
        '/health': {
          target: proxyTarget,
          changeOrigin: true,
          secure: false,
        },
      },
    },
    build: {
      sourcemap: false,
    },
  };
});
