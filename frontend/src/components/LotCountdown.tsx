import { useEffect, useState } from 'react'
import type { AuctionState } from '../types'

type Stage = 'open' | 'second' | 'final' | 'expired'

const STAGE: Record<Stage, { label: string; bar: string; text: string; ring: string }> = {
  open: {
    label: 'Bidding open',
    bar: 'bg-info-400',
    text: 'text-info-400',
    ring: 'border-info-400/40 bg-info-400/10',
  },
  second: {
    label: 'Second call',
    bar: 'bg-warn-500',
    text: 'text-warn-400',
    ring: 'border-warn-500/45 bg-warn-500/12',
  },
  final: {
    label: 'Last & final call',
    bar: 'bg-danger-500',
    text: 'text-danger-400',
    ring: 'border-danger-500/50 bg-danger-500/14',
  },
  expired: {
    label: 'Going once…',
    bar: 'bg-danger-500',
    text: 'text-danger-400',
    ring: 'border-danger-500/50 bg-danger-500/14',
  },
}

/**
 * The clock on the lot, escalating the way an auctioneer's voice does — open, second call,
 * last and final.
 *
 * It counts down to an instant the server decided, not a duration this tab started. Every
 * screen therefore shows the same number, a refresh does not restart it, and the lot still
 * closes on time if the auctioneer's browser is shut. Each bid pushes the deadline out, so a
 * raise at the death reopens the bidding rather than being hammered a second later.
 */
export function LotCountdown({ state }: { state: AuctionState }) {
  const deadline = state.auction.lotDeadlineAt
  const total = state.auction.autoTimeoutSeconds

  const [remaining, setRemaining] = useState(() => secondsLeft(deadline))

  useEffect(() => {
    if (!deadline) return

    setRemaining(secondsLeft(deadline))
    // Ticks four times a second so the number never appears to skip when the browser
    // throttles a background tab back to life.
    const timer = window.setInterval(() => setRemaining(secondsLeft(deadline)), 250)
    return () => window.clearInterval(timer)
  }, [deadline])

  if (!deadline || total <= 0 || !state.currentPlayer) return null

  const stage = stageFor(remaining, total)
  const style = STAGE[stage]
  const progress = Math.max(0, Math.min(1, remaining / total))

  return (
    <section
      className={`overflow-hidden rounded-2xl border ${style.ring}`}
      role="timer"
      aria-live="off"
      aria-label={`${style.label}, ${remaining} seconds remaining`}
    >
      <div className="flex items-center gap-4 px-4 py-3">
        <p
          className={`nums text-3xl font-black tabular-nums sm:text-4xl ${style.text} ${
            stage === 'final' ? 'animate-pulse-ring rounded-lg' : ''
          }`}
        >
          {String(Math.max(0, remaining)).padStart(2, '0')}
        </p>

        <p className={`flex-1 text-sm font-bold uppercase tracking-[0.14em] ${style.text}`}>
          {style.label}
        </p>

        <p className="nums hidden text-xs text-slate-400 sm:block">{total}s per lot</p>
      </div>

      {/* Drains left to right. Transition is linear so it moves like a clock rather than
          easing, which would read as slowing down near the end. */}
      <div className="h-1 w-full bg-white/8">
        <div
          className={`h-full ${style.bar}`}
          style={{ width: `${progress * 100}%`, transition: 'width 250ms linear' }}
        />
      </div>
    </section>
  )
}

function secondsLeft(deadline: string | null): number {
  if (!deadline) return 0
  return Math.max(0, Math.ceil((new Date(deadline).getTime() - Date.now()) / 1000))
}

/**
 * Thresholds are proportional rather than fixed, so a 20-second lot and a 2-minute lot both
 * spend the same share of their life in each stage.
 */
function stageFor(remaining: number, total: number): Stage {
  if (remaining <= 0) return 'expired'

  const share = remaining / total
  if (share <= 0.2) return 'final'
  if (share <= 0.5) return 'second'
  return 'open'
}
