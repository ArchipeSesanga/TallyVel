import react, { reactCompilerPreset } from '@vitejs/plugin-react'
import babel from '@rolldown/plugin-babel'
import { defineConfig } from 'vitest/config'

import tailwindcss from "@tailwindcss/vite";
// https://vite.dev/config/
export default defineConfig({
  plugins: [
    react(),
    tailwindcss(),
    babel({ presets: [reactCompilerPreset()] })
  ],
  // Fixed port: if 3000 is busy, fail loudly instead of silently moving to another port.
  server: { port: 3000, strictPort: true },
  // Component tests need a browser-like DOM; the older tests still run fine inside it.
  test: { environment: 'jsdom', setupFiles: ['./src/test/setup.ts'] },
})
