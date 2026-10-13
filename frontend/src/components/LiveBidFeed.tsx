import type { Bid } from '../types'
import { clockTime, hueFor, money, moneyFull } from '../lib/format'
import { EmptyState, Panel } from './primitives'

/**
 * Newest bid first. Each row is keyed by bid id, so an arriving bid mounts fresh and
 * replays the flash animation without any imperative bookkeeping.
 */
export function LiveBidFeed({ bids }: { bids: Bid[] }) {
  return (
    // No h-full: the list caps itself at max-h and scrolls, and stretching the panel to a
    // sibling column's height leaves a tall empty box on narrow screens.
    <Panel title="Live bidding">
      {bids.length === 0 ? (
        <EmptyState>Bids will appear here the moment they are placed.</EmptyState>
      ) : (
        <ol className="-mr-1 max-h-[22rem] space-y-1.5 overflow-y-auto pr-1">
          {bids.map((bid, index) => (
            <li
              key={bid.id}
              className={`glass-inset flex items-center gap-3 px-3 py-2 ${
                index === 0 ? 'animate-flash-bid' : ''
              }`}
            >
              <span
                aria-hidden
                className="size-2 shrink-0 rounded-full"
                style={{ background: `hsl(${hueFor(bid.teamName)} 72% 58%)` }}
              />
              <div className="min-w-0 flex-1">
                <p className="truncate text-sm font-semibold text-slate-100">{bid.teamName}</p>
                <p className="truncate text-xs text-slate-400">{bid.playerName}</p>
              </div>
              <div className="text-right">
                <p className="nums text-sm font-bold text-white" title={moneyFull(bid.amount)}>
                  {money(bid.amount)}
                </p>
                <p className="nums text-[0.65rem] text-slate-500">{clockTime(bid.placedAt)}</p>
              </div>
              {bid.isManualOverride && (
                <span className="pill pill-warn" title="Entered by an admin for an offline bidder">
                  off
                </span>
              )}
            </li>
          ))}
        </ol>
      )}
    </Panel>
  )
}
