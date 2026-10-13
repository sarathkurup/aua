import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

// Honour an assigned PORT so a second instance can run alongside the first. Nothing here
// depends on a fixed port: the proxy below is outbound, and the API echoes any origin in
// development — only the production CORS allow-list names ports. Strict when a port was
// handed to us, so a clash fails loudly instead of drifting onto one nobody is watching.
const port = Number(process.env.PORT) || 5173

// The dev server proxies /api and /hubs to the .NET host, so the browser only ever
// talks to one origin. That keeps CORS out of the picture in development and lets a
// phone on the same network hit the Vite LAN address and reach the API through it.
export default defineConfig({
  plugins: [react(), tailwindcss()],
  server: {
    host: true,
    port,
    strictPort: Boolean(process.env.PORT),
    proxy: {
      '/api': { target: 'http://localhost:5180', changeOrigin: true },
      '/hubs': { target: 'http://localhost:5180', changeOrigin: true, ws: true },
      // Uploaded player photos. Without this they fall through to index.html and every
      // avatar silently renders as a broken 0x0 image.
      '/media': { target: 'http://localhost:5180', changeOrigin: true },
    },
  },
})
