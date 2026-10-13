import type { Player } from '../types'

/**
 * How far through the pool the room is.
 *
 * A bare "1/10 sold" is accurate but flat — it gives no sense of how much of the evening is
 * left. The bar splits the whole pool into sold, passed and still to come, so the shape of
 * the night is readable at a glance from across a room.
 */
export function AuctionProgress({ players }: { players: Player[] }) {
  const total = players.length
  const sold = players.filter((p) => p.status === 'Sold').length
  const unsold = players.filter((p) => p.status === 'Unsold').length
  const done = sold + unsold
  const remaining = total - done

  if (total === 0) return null

  const pct = (n: number) => `${(n / total) * 100}%`

  return (
    <section className="glass p-3 sm:p-4" aria-label="Auction progress">
      <div className="flex flex-wrap items-baseline justify-between gap-2">
        <h2 className="section-title">Auction progress</h2>
        <p className="nums text-sm font-semibold text-slate-200">
          {done} / {total} completed
        </p>
      </div>

      <div
        className="mt-2 flex h-2 overflow-hidden rounded-full bg-white/8"
        role="img"
        aria-label={`${sold} sold, ${unsold} unsold, ${remaining} remaining of ${total}`}
      >
        <div
          className="h-full bg-live-500 transition-[width] duration-500"
          style={{ width: pct(sold) }}
        />
        <div
          className="h-full bg-danger-500 transition-[width] duration-500"
          style={{ width: pct(unsold) }}
        />
      </div>

      <div className="mt-2 flex flex-wrap items-center gap-x-4 gap-y-1 text-xs text-slate-400">
        <span className="flex items-center gap-1.5">
          <span aria-hidden className="size-2 rounded-full bg-live-500" />
          <span className="nums">Sold: {sold}</span>
        </span>
        <span className="flex items-center gap-1.5">
          <span aria-hidden className="size-2 rounded-full bg-danger-500" />
          <span className="nums">Unsold: {unsold}</span>
        </span>
        <span className="nums ml-auto">Remaining: {remaining}</span>
      </div>
    </section>
  )
}
