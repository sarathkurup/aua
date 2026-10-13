import { useEffect, useMemo, useRef } from 'react'
import type { Player } from '../types'
import { money, moneyFull, squadNumber } from '../lib/format'
import { PlayerPhoto } from './PlayerPhoto'

/**
 * Every sale so far, newest first.
 *
 * The squad columns answer "what does this team hold"; this answers "what just happened",
 * which is the question a room asks between lots. Both earn their place, so this sits above
 * the columns rather than replacing them.
 *
 * Ordered by sale time, not lot order — a player passed early and re-called later belongs
 * where they actually sold.
 */
export function SoldStrip({ players }: { players: Player[] }) {
  const rail = useRef<HTMLUListElement>(null)

  const sold = useMemo(
    () =>
      players
        .filter((player) => player.status === 'Sold')
        .sort((a, b) => {
          if (a.soldAt && b.soldAt) return b.soldAt.localeCompare(a.soldAt)
          // Rows predating the sale timestamp sort last rather than jumping to the front.
          if (a.soldAt) return -1
          if (b.soldAt) return 1
          return a.lotOrder - b.lotOrder
        }),
    [players],
  )

  const newestId = sold[0]?.id

  // Snap back to the front when a sale lands, so the newest card is never scrolled past.
  useEffect(() => {
    rail.current?.scrollTo({ left: 0, behavior: 'smooth' })
  }, [newestId])

  if (sold.length === 0) return null

  return (
    <section className="glass p-3 sm:p-4" aria-label={`${sold.length} players sold`}>
      <h2 className="section-title mb-2.5">Sold players ({sold.length})</h2>

      <ul
        ref={rail}
        className="-mx-1 flex gap-2 overflow-x-auto px-1 pb-1"
        style={{ scrollbarWidth: 'thin' }}
      >
        {sold.map((player, index) => (
          <li
            key={player.id}
            className={`glass-inset w-40 shrink-0 p-2.5 ${index === 0 ? 'animate-rise-in' : ''}`}
          >
            <div className="flex items-center gap-2">
              <PlayerPhoto player={player} size="sm" />
              <div className="min-w-0 flex-1">
                <p className="truncate text-xs font-semibold text-slate-100">{player.name}</p>
                <p className="nums truncate text-[0.65rem] text-slate-500">
                  {squadNumber(player)} · {player.role ?? player.sportName}
                </p>
              </div>
            </div>

            <div className="mt-2 flex items-end justify-between gap-2">
              <p className="min-w-0 truncate text-[0.65rem] uppercase tracking-[0.08em] text-slate-400">
                {player.soldToTeamName}
              </p>
              <p
                className="nums shrink-0 text-sm font-black text-live-400"
                title={moneyFull(player.soldPrice)}
              >
                {money(player.soldPrice)}
              </p>
            </div>
          </li>
        ))}
      </ul>
    </section>
  )
}
