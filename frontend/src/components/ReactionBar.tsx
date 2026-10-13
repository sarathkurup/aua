import { useCallback, useEffect, useRef, useState } from 'react'
import { Link } from 'react-router'
import { useAuth } from '../auth/AuthContext'

/** Must match the server's allow-list, which refuses anything else. */
const REACTIONS = ['🔥', '👏', '😮', '🎉', '💰'] as const

interface Floater {
  id: number
  emoji: string
  /** Horizontal offset so simultaneous cheers do not stack into one column. */
  offset: number
}

/**
 * Cheers on the current lot.
 *
 * Signed in only, matching the server: an anonymous room can hold hundreds of spectators and
 * an open relay would be noise rather than atmosphere. Everyone still *sees* the reactions —
 * only sending needs an account.
 */
export function ReactionBar({
  onSend,
  incoming,
}: {
  onSend: (emoji: string) => void
  /** The last reaction received, or null. Identity changes on every arrival. */
  incoming: { emoji: string; at: number } | null
}) {
  const { isAuthenticated } = useAuth()
  const [floaters, setFloaters] = useState<Floater[]>([])
  const nextId = useRef(1)

  const float = useCallback((emoji: string) => {
    const id = nextId.current++
    setFloaters((current) => [
      // Capped: a room mashing the button should not grow the DOM without bound.
      ...current.slice(-14),
      { id, emoji, offset: Math.random() * 70 + 10 },
    ])
    window.setTimeout(() => setFloaters((current) => current.filter((f) => f.id !== id)), 2200)
  }, [])

  useEffect(() => {
    if (incoming) float(incoming.emoji)
  }, [incoming, float])

  return (
    <div className="relative">
      {/* Floats rise out of the bar and are purely decorative, so they never take pointer
          events or announce themselves to a screen reader. */}
      <div aria-hidden className="pointer-events-none absolute inset-x-0 bottom-full h-24 overflow-hidden">
        {floaters.map((floater) => (
          <span
            key={floater.id}
            className="animate-float-up absolute bottom-0 text-2xl"
            style={{ left: `${floater.offset}%` }}
          >
            {floater.emoji}
          </span>
        ))}
      </div>

      <div className="glass flex flex-wrap items-center gap-2 p-2.5">
        <span className="section-title">Reactions</span>

        {isAuthenticated ? (
          <div className="flex flex-wrap gap-1.5">
            {REACTIONS.map((emoji) => (
              <button
                key={emoji}
                type="button"
                className="btn btn-sm text-base"
                onClick={() => {
                  onSend(emoji)
                  // Show your own cheer immediately rather than waiting for the round trip.
                  float(emoji)
                }}
                aria-label={`React with ${emoji}`}
              >
                {emoji}
              </button>
            ))}
          </div>
        ) : (
          <p className="text-xs text-slate-400">
            <Link to="/login" className="font-semibold text-brand-300 hover:text-brand-200">
              Sign in
            </Link>{' '}
            to react.
          </p>
        )}
      </div>
    </div>
  )
}
