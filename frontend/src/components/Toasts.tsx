import { createContext, useCallback, useContext, useMemo, useRef, useState } from 'react'
import type { ReactNode } from 'react'

type ToastTone = 'info' | 'success' | 'warn' | 'error'

interface Toast {
  id: number
  tone: ToastTone
  message: string
}

interface ToastContextValue {
  push: (message: string, tone?: ToastTone) => void
}

const ToastContext = createContext<ToastContextValue | null>(null)

const toneClass: Record<ToastTone, string> = {
  info: 'pill-brand',
  success: 'pill-live',
  warn: 'pill-warn',
  error: 'pill-danger',
}

export function ToastProvider({ children }: { children: ReactNode }) {
  const [toasts, setToasts] = useState<Toast[]>([])
  const nextId = useRef(1)

  const push = useCallback((message: string, tone: ToastTone = 'info') => {
    const id = nextId.current++
    setToasts((current) => [...current.slice(-3), { id, tone, message }])
    window.setTimeout(() => setToasts((current) => current.filter((t) => t.id !== id)), 4200)
  }, [])

  const value = useMemo(() => ({ push }), [push])

  return (
    <ToastContext value={value}>
      {children}
      <div
        className="pointer-events-none fixed inset-x-0 bottom-4 z-50 flex flex-col items-center gap-2 px-4 sm:bottom-6"
        role="status"
        aria-live="polite"
      >
        {toasts.map((toast) => (
          <div
            key={toast.id}
            className="glass-strong animate-rise-in pointer-events-auto flex max-w-md items-center gap-3 px-4 py-3 text-sm"
          >
            <span className={`pill ${toneClass[toast.tone]}`}>{toast.tone}</span>
            <span className="text-slate-100">{toast.message}</span>
          </div>
        ))}
      </div>
    </ToastContext>
  )
}

export function useToast(): ToastContextValue {
  const context = useContext(ToastContext)
  if (!context) throw new Error('useToast must be used inside <ToastProvider>')
  return context
}
