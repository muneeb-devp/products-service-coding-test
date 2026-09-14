/// <reference types="vitest/config" />
import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

export default defineConfig({
  plugins: [react(), tailwindcss()],
  server: {
    port: 5173,
    // Bind on all interfaces so the container-hosted dev server is reachable
    // from the host machine.
    host: true,
  },
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: './src/test/setup.ts',
    css: true,
    coverage: {
      provider: 'v8',
      reporter: ['text', 'lcov'],
      // Config, entry point and type-only files have no behaviour to assert.
      exclude: [
        '**/*.config.*',
        '**/src/main.tsx',
        '**/src/test/**',
        '**/types/**',
        '**/dist/**',
        '**/node_modules/**',
      ],
    },
  },
})
