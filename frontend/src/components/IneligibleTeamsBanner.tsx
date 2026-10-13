import { useEffect, useState } from 'react'
import type { AuctionState, TeamSummary } from '../types'
import { money } from '../lib/format'

type Reason = 'purse' | 'squad' | 'quota'

interface Ineligible {
  team: TeamSummary
  reason: Reason
}

const REASON_LABEL: Record<Reason, string> = {
  purse: 'cannot afford the next bid',
  squad: 'squad is full',
  quota: 'must keep its purse for the women it still owes',
}

/**
 * Which teams are out of the running for the lot on the block.
 *
 * Every input is already in the room state — it was just buried one per team column, where
 * nobody reads it mid-lot. Pulling it to the top answers the question the auctioneer is
 * actually asking as the price climbs: who is still live on this player?
 *
 * Re-evaluated against `nextMinimumBid`, so a team drops out of contention at the moment the
 * bidding passes what it can commit.
 */
export function IneligibleTeamsBanner({ state }: { state: AuctionState }) {
  const [dismissed, setDismissed] = useState(false)

  // A dismissal applies to the lot it was made on. The next player is a fresh question.
  useEffect(() => setDismissed(false), [state.auction.currentPlayerId])

  if (!state.currentPlayer || dismissed) return null

  const ineligible: Ineligible[] = state.teams
    .filter((team) => !team.canBid || team.spendablePurse < state.nextMinimumBid)
    .map((team) => ({ team, reason: reasonFor(team, state) }))

  if (ineligible.length === 0 || ineligible.length === state.teams.length) {
    // All-out is not a useful banner — it means the lot is finished, which the
    // auctioneer's controls already say.
    return null
  }

  const grouped = groupByReason(ineligible)

  return (
    <section
      className="animate-rise-in rounded-2xl border border-danger-500/35 bg-danger-500/10 px-3 py-2.5 sm:px-4"
      role="status"
      aria-live="polite"
    >
      <div className="flex items-start gap-3">
        <span
          aria-hidden
          className="mt-0.5 grid size-5 shrink-0 place-items-center rounded-full bg-danger-500/25 text-xs font-black text-danger-400"
        >
          !
        </span>

        <div className="min-w-0 flex-1 space-y-1.5">
          {grouped.map(([reason, teams]) => (
            <p key={reason} className="flex flex-wrap items-center gap-x-2 gap-y-1 text-sm">
              <span className="font-semibold text-danger-400">Not eligible:</span>
              {teams.map(({ team }) => (
                <span
                  key={team.teamId}
                  className="pill pill-danger"
                  title={
                    reason === 'purse'
                      ? `Can commit ${money(team.spendablePurse)}; the next bid is ${money(state.nextMinimumBid)}`
                      : (team.blockedReason ?? REASON_LABEL[reason])
                  }
                >
                  {team.name}
                </span>
              ))}
              <span className="text-xs text-slate-400">— {REASON_LABEL[reason]}</span>
            </p>
          ))}
        </div>

        <button
          type="button"
          className="shrink-0 rounded-lg px-2 py-1 text-sm text-slate-400 transition hover:text-slate-100"
          onClick={() => setDismissed(true)}
          aria-label="Dismiss for this lot"
        >
          ✕
        </button>
      </div>
    </section>
  )
}

function reasonFor(team: TeamSummary, state: AuctionState): Reason {
  if (!team.canBid) return 'squad'

  // A team with purse in hand but nothing spendable is being held back by the reserve, which
  // in a corporate tournament means the women it still has to sign.
  if (team.womenRequired > 0 && team.purse >= state.nextMinimumBid) return 'quota'

  return 'purse'
}

/** Keeps the reason order stable so the banner does not reshuffle as bids land. */
function groupByReason(items: Ineligible[]): Array<[Reason, Ineligible[]]> {
  const order: Reason[] = ['purse', 'squad', 'quota']
  return order
    .map((reason): [Reason, Ineligible[]] => [reason, items.filter((i) => i.reason === reason)])
    .filter(([, teams]) => teams.length > 0)
}
