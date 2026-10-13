import type { AuctionState } from '../types'
import { money, moneyFull } from '../lib/format'
import { EmptyState, GenderTag, Panel, SkillList } from './primitives'
import { PlayerPhoto } from './PlayerPhoto'

/**
 * The lot currently under the hammer. This is the focal point of every screen, so it gets
 * the strongest glass treatment and the largest numerals in the app.
 */
export function OnBlockCard({ state }: { state: AuctionState }) {
  const player = state.currentPlayer

  if (!player) {
    return (
      <Panel title="On the block" strong>
        <EmptyState>
          {state.auction.status === 'Live'
            ? 'No lot called yet. The auctioneer will bring up the next player.'
            : 'The auction is not running.'}
        </EmptyState>
      </Panel>
    )
  }

  const leading = state.topBid

  return (
    <Panel title="On the block" strong className="overflow-hidden">
      {/* Keyed on the player so a new lot re-mounts and replays the entrance. */}
      <div key={player.id} className="animate-rise-in flex flex-col gap-5 sm:flex-row sm:items-start">
        <div className="flex items-center gap-4 sm:w-40 sm:flex-col sm:items-center sm:gap-3">
          <PlayerPhoto player={player} size="xl" className="shadow-2xl" />
          <div className="min-w-0 sm:text-center">
            <p className="text-xl font-bold leading-tight text-white">{player.name}</p>
            <p className="text-sm text-slate-400">
              {[player.role, player.country].filter(Boolean).join(' · ') || player.sportName}
            </p>
            <div className="mt-1.5 flex flex-wrap gap-1.5 sm:justify-center">
              <GenderTag gender={player.gender} />
            </div>
          </div>
        </div>

        <div className="flex-1 space-y-4">
          <div className="glass-inset px-4 py-3">
            <p className="text-[0.65rem] font-semibold uppercase tracking-[0.12em] text-slate-400">
              {leading ? 'Current bid' : 'Opening at base price'}
            </p>
            <p
              className="nums mt-1 text-4xl font-black text-white sm:text-5xl"
              title={moneyFull(leading?.amount ?? player.basePrice)}
            >
              {money(leading?.amount ?? player.basePrice)}
            </p>
            {leading ? (
              <p className="mt-1 flex flex-wrap items-center gap-2 text-sm text-slate-300">
                <span className="font-semibold text-brand-300">{leading.teamName}</span>
                <span className="text-slate-500">leads</span>
                {leading.isManualOverride && (
                  <span className="pill pill-warn" title="Entered by an admin for an offline bidder">
                    offline entry
                  </span>
                )}
              </p>
            ) : (
              <p className="mt-1 text-sm text-slate-400">No bids yet.</p>
            )}
          </div>

          <SkillList skills={player.skills} />

          <dl className="grid grid-cols-2 gap-3 text-sm">
            <div>
              <dt className="text-[0.65rem] font-semibold uppercase tracking-[0.1em] text-slate-400">
                Base price
              </dt>
              <dd className="nums font-semibold text-slate-100" title={moneyFull(player.basePrice)}>
                {money(player.basePrice)}
              </dd>
            </div>
            <div>
              <dt className="text-[0.65rem] font-semibold uppercase tracking-[0.1em] text-slate-400">
                Next minimum
              </dt>
              <dd
                className="nums font-semibold text-live-400"
                title={moneyFull(state.nextMinimumBid)}
              >
                {money(state.nextMinimumBid)}
              </dd>
            </div>
          </dl>
        </div>
      </div>
    </Panel>
  )
}
