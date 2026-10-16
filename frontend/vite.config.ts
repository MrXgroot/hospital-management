/// <reference types="vitest/config" />
import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    // The API's plain-HTTP dev endpoint avoids dev-certificate trust issues in the proxy.
    proxy: { '/api': { target: 'http://localhost:5001', changeOrigin: true } },
  },
  build: {
    rollupOptions: {
      output: {
        // Long-lived vendor chunks cache well across app deploys.
        manualChunks: {
          react: ['react', 'react-dom', 'react-router'],
          mui: ['@mui/material', '@emotion/react', '@emotion/styled'],
          data: ['@tanstack/react-query', 'axios', 'dayjs'],
        },
      },
    },
  },
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: ['./src/test/setup.ts'],
    css: false,
  },
});
