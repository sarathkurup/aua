import { useEffect, useState } from 'react'
import { createPortal } from 'react-dom'
import type { Player } from '../types'
import { money } from '../lib/format'
import { PlayerPhoto } from './PlayerPhoto'

export interface FlightRects {
  from: DOMRect
  to: DOMRect
}

/**
 * The sold player's card flying from the block into the team that bought them.
 *
 * Rendered into a portal at fixed coordinates so it can travel across the whole page
 * without the surrounding layout clipping it, and it is purely decorative — the real card
 * has already appeared in the team column underneath, driven by the state broadcast. If
 * this never rendered, nothing would be lost but the sense of movement.
 */
export function SoldFlight({
  player,
  from,
  to,
  onDone,
}: FlightRects & { player: Player; onDone: () => void }) {
  const [arrived, setArrived] = useState(false)

  useEffect(() => {
    // One frame at the start position, so the browser has something to animate from.
    const frame = requestAnimationFrame(() => setArrived(true))
    return () => cancelAnimationFrame(frame)
  }, [])

  const dx = to.left + to.width / 2 - (from.left + from.width / 2)
  const dy = to.top + 24 - from.top

  return createPortal(
    <div
      aria-hidden
      onTransitionEnd={onDone}
      className="glass-strong pointer-events-none fixed z-50 flex items-center gap-3 p-3"
      style={{
        left: from.left,
        top: from.top,
        width: from.width,
        transformOrigin: 'center top',
        transition: 'transform 720ms cubic-bezier(0.34, 0.8, 0.3, 1), opacity 720ms ease-out',
        transform: arrived
          ? `translate(${dx}px, ${dy}px) scale(0.34)`
          : 'translate(0px, 0px) scale(1)',
        opacity: arrived ? 0 : 1,
      }}
    >
      <PlayerPhoto player={player} size="md" />
      <div className="min-w-0 flex-1">
        <p className="truncate text-base font-bold text-white">{player.name}</p>
        <p className="truncate text-sm text-slate-300">{player.soldToTeamName}</p>
      </div>
      <p className="nums text-xl font-black text-live-400">{money(player.soldPrice)}</p>
    </div>,
    document.body,
  )
}
