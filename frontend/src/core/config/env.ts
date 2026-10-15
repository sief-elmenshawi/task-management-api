const value: unknown = import.meta.env.VITE_API_URL

if (typeof value !== 'string' || value.trim() === '') {
  throw new Error(
    'Missing VITE_API_URL. Copy .env.example to .env and set VITE_API_URL=http://localhost:8080.',
  )
}

export const apiUrl: string = value.replace(/\/+$/, '')
