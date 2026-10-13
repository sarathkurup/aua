import type { BidIncrementRule, BiddingRuleFields } from '../types'
import { MAX_LOT_TIMEOUT_SECONDS } from '../types'
import { money } from '../lib/format'

/**
 * How bidding behaves in this room: where it opens, how fast it climbs, and how long a lot
 * stays alive.
 *
 * The ladder is the part worth getting right. A flat step is wrong at both ends of an
 * auction — ₹1,000 raises are far too slow once a lot is in five figures, and ₹5,000 steps
 * price a room out of its cheap players. So the step widens at thresholds the organiser sets.
 */
export function BiddingRulesFields({
  value,
  onChange,
  idPrefix,
}: {
  value: BiddingRuleFields
  onChange: (next: BiddingRuleFields) => void
  /** Keeps label/input ids unique when the create and edit forms are both mounted. */
  idPrefix: string
}) {
  const ladder = value.increments

  function setLadder(next: BidIncrementRule[]) {
    onChange({ ...value, increments: [...next].sort((a, b) => a.from - b.from) })
  }

  function updateRung(index: number, patch: Partial<BidIncrementRule>) {
    setLadder(ladder.map((rung, i) => (i === index ? { ...rung, ...patch } : rung)))
  }

  function addRung() {
    const last = ladder[ladder.length - 1]
    const from = last ? Math.max(last.from * 2, last.from + last.increment * 10) : 10_000
    const increment = last ? last.increment * 2 : 1_000
    setLadder([...ladder, { from, increment }])
  }

  return (
    <div className="space-y-3">
      <div className="grid gap-3 sm:grid-cols-3">
        <div>
          <label className="field-label" htmlFor={`${idPrefix}-minimum`}>
            Minimum bid
          </label>
          <input
            id={`${idPrefix}-minimum`}
            className="field nums"
            type="number"
            min={0}
            step={1}
            value={value.minimumBid}
            onChange={(event) => onChange({ ...value, minimumBid: Number(event.target.value) || 0 })}
          />
          <p className="mt-1 text-xs text-slate-500">
            Floor for the opening bid. A player whose base price is lower still opens here.
          </p>
        </div>

        <div>
          <label className="field-label" htmlFor={`${idPrefix}-increment`}>
            Base increment
          </label>
          <input
            id={`${idPrefix}-increment`}
            className="field nums"
            type="number"
            min={1}
            step={1}
            value={value.minBidIncrement}
            onChange={(event) =>
              onChange({ ...value, minBidIncrement: Number(event.target.value) || 1 })
            }
            required
          />
          <p className="mt-1 text-xs text-slate-500">Used when no ladder rung applies.</p>
        </div>

        <div>
          <label className="field-label" htmlFor={`${idPrefix}-timeout`}>
            Seconds per lot
          </label>
          <input
            id={`${idPrefix}-timeout`}
            className="field nums"
            type="number"
            min={0}
            max={MAX_LOT_TIMEOUT_SECONDS}
            step={5}
            value={value.autoTimeoutSeconds}
            onChange={(event) =>
              onChange({ ...value, autoTimeoutSeconds: Number(event.target.value) || 0 })
            }
          />
          <p className="mt-1 text-xs text-slate-500">
            Each bid resets it. <strong>0</strong> keeps every lot open until you call it.
          </p>
        </div>
      </div>

      <fieldset className="glass-inset p-3">
        <legend className="field-label">Increment ladder</legend>

        <div className="space-y-2">
          {ladder.map((rung, index) => (
            <div key={index} className="flex flex-wrap items-center gap-2">
              <span className="text-xs text-slate-400">From</span>
              <input
                className="field nums w-28 py-1.5 text-xs"
                type="number"
                min={0}
                step={1}
                value={rung.from}
                onChange={(event) => updateRung(index, { from: Number(event.target.value) || 0 })}
                aria-label={`Rung ${index + 1} threshold`}
              />
              <span className="text-xs text-slate-400">raise by</span>
              <input
                className="field nums w-28 py-1.5 text-xs"
                type="number"
                min={1}
                step={1}
                value={rung.increment}
                onChange={(event) =>
                  updateRung(index, { increment: Number(event.target.value) || 1 })
                }
                aria-label={`Rung ${index + 1} increment`}
              />

              <button
                type="button"
                className="btn btn-sm btn-danger"
                // The first rung is what applies from zero; without it a lot below every
                // other threshold would have no step at all.
                disabled={ladder.length === 1}
                onClick={() => setLadder(ladder.filter((_, i) => i !== index))}
                aria-label={`Remove rung ${index + 1}`}
              >
                Remove
              </button>
            </div>
          ))}
        </div>

        <button type="button" className="btn btn-sm mt-2" onClick={addRung}>
          Add a step
        </button>

        <p className="mt-2 text-xs leading-relaxed text-slate-400">
          {describe(ladder)}
        </p>
      </fieldset>
    </div>
  )
}

/** Reads the ladder back as a sentence, so a mis-entered threshold is obvious before saving. */
function describe(ladder: BidIncrementRule[]): string {
  if (ladder.length === 0) return 'No rungs — the base increment applies throughout.'

  const sorted = [...ladder].sort((a, b) => a.from - b.from)
  return sorted
    .map((rung, index) => {
      const next = sorted[index + 1]
      const range = next
        ? `${money(rung.from)}–${money(next.from)}`
        : `${money(rung.from)} and above`
      return `${range}: +${money(rung.increment)}`
    })
    .join(' · ')
}

/** Sensible starting ladder for a new auction — the shape most leagues end up with. */
export function defaultBiddingRules(increment: number): BiddingRuleFields {
  return {
    minBidIncrement: increment,
    minimumBid: 0,
    increments: [{ from: 0, increment }],
    autoTimeoutSeconds: 0,
  }
}
