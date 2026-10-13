import { useEffect, useState } from 'react'
import { createPortal } from 'react-dom'
import type { Player } from '../types'
import { money, moneyFull, squadNumber } from '../lib/format'
import { PlayerPhoto } from './PlayerPhoto'

/**
 * Full-screen celebration when a lot is hammered down.
 *
 * The alternative to the card flight, and the better one on a projector: a card travelling
 * across a hall is lost, whereas this reads from the back of a room. Chosen in settings,
 * because which is right depends entirely on the screen it is shown on.
 *
 * Decorative only — the sale is already recorded and the squad column already updated
 * beneath it, so dismissing early or never seeing it costs nothing.
 */
export function SoldOverlay({ player, onDone }: { player: Player; onDone: () => void }) {
  const [shown, setShown] = useState(false)

  useEffect(() => {
    const frame = requestAnimationFrame(() => setShown(true))
    const timer = window.setTimeout(onDone, 2600)

    return () => {
      cancelAnimationFrame(frame)
      window.clearTimeout(timer)
    }
  }, [onDone])

  return createPortal(
    <div
      className="pointer-events-none fixed inset-0 z-50 grid place-items-center px-4"
      style={{
        background: `rgba(2, 6, 23, ${shown ? 0.72 : 0})`,
        transition: 'background 260ms ease-out',
      }}
      role="status"
      aria-live="polite"
    >
      <div
        className="glass-strong w-full max-w-sm p-6 text-center sm:p-8"
        style={{
          borderColor: 'rgba(52, 211, 153, 0.45)',
          boxShadow: '0 24px 80px rgba(16, 185, 129, 0.28)',
          transform: shown ? 'scale(1) translateY(0)' : 'scale(0.9) translateY(12px)',
          opacity: shown ? 1 : 0,
          transition: 'transform 340ms cubic-bezier(0.2, 1.1, 0.35, 1), opacity 240ms ease-out',
        }}
      >
        <p
          className="mx-auto w-fit rounded-full px-6 py-2 text-2xl font-black tracking-[0.12em] text-white sm:text-3xl"
          style={{ background: 'linear-gradient(135deg, #10b981, #059669)' }}
        >
          SOLD!
        </p>

        <div className="mt-5 flex justify-center">
          <PlayerPhoto player={player} size="xl" className="shadow-2xl" />
        </div>

        <p className="mt-4 text-2xl font-black leading-tight text-white sm:text-3xl">
          {player.name}{' '}
          <span className="nums text-lg font-bold text-slate-400">{squadNumber(player)}</span>
        </p>
        <p className="text-sm text-slate-300">{player.role ?? player.sportName}</p>

        <p className="mt-4 text-base font-semibold text-brand-300">{player.soldToTeamName}</p>
        <p
          className="nums mt-1 text-4xl font-black text-live-400 sm:text-5xl"
          title={moneyFull(player.soldPrice)}
        >
          {money(player.soldPrice)}
        </p>
      </div>
    </div>,
    document.body,
  )
}
