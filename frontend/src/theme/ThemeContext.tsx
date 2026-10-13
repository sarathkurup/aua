import { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react'
import type { ReactNode } from 'react'

/** What the user picked. `system` follows the OS setting and keeps following it. */
export type ThemeMode = 'dark' | 'light' | 'system'

/** What is actually on screen once `system` has been resolved. */
export type ResolvedTheme = 'dark' | 'light'

const STORAGE_KEY = 'auction.theme'

interface ThemeContextValue {
  mode: ThemeMode
  resolved: ResolvedTheme
  setMode: (mode: ThemeMode) => void
  /** Flips between dark and light, leaving `system` behind. */
  toggle: () => void
}

const ThemeContext = createContext<ThemeContextValue | null>(null)

function systemTheme(): ResolvedTheme {
  return window.matchMedia('(prefers-color-scheme: light)').matches ? 'light' : 'dark'
}

function storedMode(): ThemeMode {
  const saved = localStorage.getItem(STORAGE_KEY)
  return saved === 'dark' || saved === 'light' || saved === 'system' ? saved : 'system'
}

/**
 * Applies the theme by stamping `data-theme` on the root element, which every
 * token in index.css keys off. The same stamp is applied by a small script in
 * index.html before first paint, so the page never flashes the wrong theme.
 */
export function ThemeProvider({ children }: { children: ReactNode }) {
  const [mode, setModeState] = useState<ThemeMode>(() => storedMode())
  const [resolved, setResolved] = useState<ResolvedTheme>(() =>
    mode === 'system' ? systemTheme() : mode,
  )

  useEffect(() => {
    const next = mode === 'system' ? systemTheme() : mode
    setResolved(next)
    document.documentElement.dataset.theme = next
    localStorage.setItem(STORAGE_KEY, mode)
  }, [mode])

  // Only while following the system: a user who picked a theme explicitly keeps it.
  useEffect(() => {
    if (mode !== 'system') return

    const query = window.matchMedia('(prefers-color-scheme: light)')
    const onChange = () => {
      const next = systemTheme()
      setResolved(next)
      document.documentElement.dataset.theme = next
    }

    query.addEventListener('change', onChange)
    return () => query.removeEventListener('change', onChange)
  }, [mode])

  const setMode = useCallback((next: ThemeMode) => setModeState(next), [])

  const toggle = useCallback(
    () => setModeState(resolved === 'dark' ? 'light' : 'dark'),
    [resolved],
  )

  const value = useMemo(() => ({ mode, resolved, setMode, toggle }), [mode, resolved, setMode, toggle])

  return <ThemeContext value={value}>{children}</ThemeContext>
}

export function useTheme(): ThemeContextValue {
  const context = useContext(ThemeContext)
  if (!context) throw new Error('useTheme must be used inside <ThemeProvider>')
  return context
}
