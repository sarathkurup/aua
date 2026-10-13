import { useEffect, useState } from 'react'
import type { FormEvent } from 'react'
import type { SaveTournamentRequest, Sport, Tournament, TournamentType } from '../types'
import { moneyFull } from '../lib/format'

/**
 * Defaults per tournament type. Picking "company league" should produce a working rule set
 * immediately — the participation requirement is the whole reason that type exists — while
 * still leaving every number editable.
 */
const PRESETS: Record<TournamentType, Partial<SaveTournamentRequest>> = {
  Professional: {
    minSquadSize: 3,
    maxSquadSize: 8,
    minWomenPerSquad: 0,
    maxSpendPerPlayer: null,
    reservePurseForUnfilledSlots: true,
  },
  Corporate: {
    minSquadSize: 4,
    maxSquadSize: 6,
    minWomenPerSquad: 2,
    reservePurseForUnfilledSlots: true,
  },
}

export function TournamentForm({
  tournament,
  sports,
  onSave,
  onCancel,
}: {
  /** Null creates a new tournament. */
  tournament: Tournament | null
  sports: Sport[]
  onSave: (body: SaveTournamentRequest) => Promise<void>
  onCancel: () => void
}) {
  const [name, setName] = useState('')
  const [description, setDescription] = useState('')
  const [sportId, setSportId] = useState<number | ''>('')
  const [type, setType] = useState<TournamentType>('Professional')
  const [teamPurse, setTeamPurse] = useState('100000000')
  const [minSquadSize, setMinSquadSize] = useState('0')
  const [maxSquadSize, setMaxSquadSize] = useState('0')
  const [minWomen, setMinWomen] = useState('0')
  const [maxSpend, setMaxSpend] = useState('')
  const [reserve, setReserve] = useState(true)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    setName(tournament?.name ?? '')
    setDescription(tournament?.description ?? '')
    setSportId(tournament?.sportId ?? sports[0]?.id ?? '')
    setType(tournament?.type ?? 'Professional')
    setTeamPurse(String(tournament?.teamPurse ?? 100_000_000))
    setMinSquadSize(String(tournament?.rules.minSquadSize ?? 0))
    setMaxSquadSize(String(tournament?.rules.maxSquadSize ?? 0))
    setMinWomen(String(tournament?.rules.minWomenPerSquad ?? 0))
    setMaxSpend(tournament?.rules.maxSpendPerPlayer?.toString() ?? '')
    setReserve(tournament?.rules.reservePurseForUnfilledSlots ?? true)
    setError(null)
  }, [tournament, sports])

  /** Switching type on a new tournament pulls in that type's defaults. */
  function applyType(next: TournamentType) {
    setType(next)
    if (tournament) return

    const preset = PRESETS[next]
    setMinSquadSize(String(preset.minSquadSize ?? 0))
    setMaxSquadSize(String(preset.maxSquadSize ?? 0))
    setMinWomen(String(preset.minWomenPerSquad ?? 0))
    setReserve(preset.reservePurseForUnfilledSlots ?? true)
    if (next === 'Corporate') setTeamPurse('5000000')
  }

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    if (sportId === '') {
      setError('Pick a sport.')
      return
    }

    setBusy(true)
    setError(null)

    try {
      await onSave({
        name: name.trim(),
        description: description.trim() || null,
        sportId,
        type,
        teamPurse: Number(teamPurse),
        minSquadSize: Number(minSquadSize),
        maxSquadSize: Number(maxSquadSize),
        minWomenPerSquad: Number(minWomen),
        maxSpendPerPlayer: maxSpend.trim() === '' ? null : Number(maxSpend),
        reservePurseForUnfilledSlots: reserve,
      })
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not save the tournament.')
    } finally {
      setBusy(false)
    }
  }

  return (
    <form onSubmit={handleSubmit} className="glass-inset animate-rise-in space-y-4 p-4">
      <div className="grid gap-3 sm:grid-cols-2">
        <div>
          <label className="field-label" htmlFor="t-name">
            Name
          </label>
          <input
            id="t-name"
            className="field"
            value={name}
            onChange={(event) => setName(event.target.value)}
            required
            autoFocus
          />
        </div>

        <div>
          <label className="field-label" htmlFor="t-sport">
            Sport
          </label>
          <select
            id="t-sport"
            className="field"
            value={sportId}
            onChange={(event) => setSportId(Number(event.target.value))}
            required
            disabled={tournament !== null}
          >
            <option value="">Choose…</option>
            {sports.map((sport) => (
              <option key={sport.id} value={sport.id}>
                {sport.icon} {sport.name}
              </option>
            ))}
          </select>
        </div>

        <div className="sm:col-span-2">
          <label className="field-label" htmlFor="t-description">
            Description
          </label>
          <input
            id="t-description"
            className="field"
            value={description}
            onChange={(event) => setDescription(event.target.value)}
            placeholder="Shown on the public listing"
          />
        </div>
      </div>

      <fieldset>
        <legend className="field-label">Rule book</legend>
        <div className="flex flex-wrap gap-2">
          {(['Professional', 'Corporate'] as const).map((option) => (
            <button
              key={option}
              type="button"
              className={`btn btn-sm ${type === option ? 'btn-primary' : ''}`}
              aria-pressed={type === option}
              onClick={() => applyType(option)}
            >
              {option === 'Corporate' ? 'Company league' : 'Professional'}
            </button>
          ))}
        </div>
        <p className="mt-1.5 text-xs text-slate-500">
          {type === 'Corporate'
            ? 'Workplace rules: squads must include a minimum number of women.'
            : 'Open competition. Squad size limits only.'}
        </p>
      </fieldset>

      <div className="grid gap-3 sm:grid-cols-3">
        <div>
          <label className="field-label" htmlFor="t-purse">
            Purse per team
          </label>
          <input
            id="t-purse"
            className="field nums"
            type="number"
            min={1}
            step={1}
            value={teamPurse}
            onChange={(event) => setTeamPurse(event.target.value)}
            required
          />
          <p className="nums mt-1 text-xs text-slate-500">{moneyFull(Number(teamPurse) || 0)}</p>
        </div>

        <div>
          <label className="field-label" htmlFor="t-min-squad">
            Min squad size
          </label>
          <input
            id="t-min-squad"
            className="field nums"
            type="number"
            min={0}
            step={1}
            value={minSquadSize}
            onChange={(event) => setMinSquadSize(event.target.value)}
          />
        </div>

        <div>
          <label className="field-label" htmlFor="t-max-squad">
            Max squad size
          </label>
          <input
            id="t-max-squad"
            className="field nums"
            type="number"
            min={0}
            step={1}
            value={maxSquadSize}
            onChange={(event) => setMaxSquadSize(event.target.value)}
          />
        </div>

        <div>
          <label className="field-label" htmlFor="t-min-women">
            Min women per squad
          </label>
          <input
            id="t-min-women"
            className="field nums"
            type="number"
            min={0}
            step={1}
            value={minWomen}
            onChange={(event) => setMinWomen(event.target.value)}
          />
        </div>

        <div className="sm:col-span-2">
          <label className="field-label" htmlFor="t-max-spend">
            Max spend on one player
          </label>
          <input
            id="t-max-spend"
            className="field nums"
            type="number"
            min={0}
            step={1}
            value={maxSpend}
            onChange={(event) => setMaxSpend(event.target.value)}
            placeholder="Leave blank for no cap"
          />
        </div>
      </div>

      <label className="flex items-start gap-2 text-sm text-slate-300">
        <input
          type="checkbox"
          className="mt-0.5"
          checked={reserve}
          onChange={(event) => setReserve(event.target.checked)}
        />
        <span>
          Hold back purse for unfilled slots
          <span className="block text-xs text-slate-500">
            Refuses a bid that would leave a team unable to complete its squad at the cheapest
            price left in the pool. Without this, a minimum-women rule cannot actually be enforced.
          </span>
        </span>
      </label>

      {error && (
        <p className="text-sm text-danger-400" role="alert">
          {error}
        </p>
      )}

      <div className="flex justify-end gap-2">
        <button type="button" className="btn btn-sm" onClick={onCancel} disabled={busy}>
          Cancel
        </button>
        <button type="submit" className="btn btn-sm btn-primary" disabled={busy}>
          {busy ? 'Saving…' : tournament ? 'Save rules' : 'Create tournament'}
        </button>
      </div>
    </form>
  )
}
