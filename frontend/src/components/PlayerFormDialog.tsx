import { useEffect, useRef, useState } from 'react'
import type { ChangeEvent, FormEvent } from 'react'
import { errorMessage } from '../api/client'
import { playersApi } from '../api/endpoints'
import type { Gender, Player, SavePlayerRequest } from '../types'
import { PlayerPhoto } from './PlayerPhoto'

interface PlayerFormDialogProps {
  /** Null creates a new player; a player edits that one. */
  player: Player | null
  tournamentId: number
  /** Attribute names for this tournament's sport, e.g. ["Batting","Bowling"]. */
  skillFields: string[]
  /** True when the tournament has a women-per-squad rule, which makes gender required. */
  genderRequired: boolean
  /** Suggested lot number for a new entry — one past the current pool. */
  nextLotOrder: number
  onSave: (body: SavePlayerRequest) => Promise<Player>
  onClose: () => void
}

const GENDERS: Array<{ value: Gender; label: string }> = [
  { value: 'Unspecified', label: 'Not recorded' },
  { value: 'Male', label: 'Man' },
  { value: 'Female', label: 'Woman' },
]

export function PlayerFormDialog({
  player,
  tournamentId,
  skillFields,
  genderRequired,
  nextLotOrder,
  onSave,
  onClose,
}: PlayerFormDialogProps) {
  const [name, setName] = useState('')
  const [role, setRole] = useState('')
  const [gender, setGender] = useState<Gender>('Unspecified')
  const [country, setCountry] = useState('')
  const [skills, setSkills] = useState<Record<string, string>>({})
  const [basePrice, setBasePrice] = useState('0')
  const [lotOrder, setLotOrder] = useState(String(nextLotOrder))
  const [jerseyNumber, setJerseyNumber] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  /** Set after a save so the photo can be attached to a player that now has an id. */
  const [saved, setSaved] = useState<Player | null>(player)
  const [uploading, setUploading] = useState(false)
  const fileRef = useRef<HTMLInputElement>(null)

  useEffect(() => {
    setName(player?.name ?? '')
    setRole(player?.role ?? '')
    setGender(player?.gender ?? 'Unspecified')
    setCountry(player?.country ?? '')
    setSkills(player?.skills ?? {})
    setBasePrice(String(player?.basePrice ?? 0))
    setLotOrder(String(player?.lotOrder ?? nextLotOrder))
    setJerseyNumber(player?.jerseyNumber === null || player?.jerseyNumber === undefined ? '' : String(player.jerseyNumber))
    setSaved(player)
    setError(null)
  }, [player, nextLotOrder])

  // Escape closes, matching the behaviour of the buttons around it.
  useEffect(() => {
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') onClose()
    }
    window.addEventListener('keydown', onKeyDown)
    return () => window.removeEventListener('keydown', onKeyDown)
  }, [onClose])

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()

    if (genderRequired && gender === 'Unspecified') {
      setError('This tournament counts women per squad, so every player needs a gender.')
      return
    }

    setBusy(true)
    setError(null)

    try {
      const result = await onSave({
        name: name.trim(),
        tournamentId,
        role: role.trim() || null,
        gender,
        country: country.trim() || null,
        skills,
        basePrice: Number(basePrice),
        imageUrl: null,
        lotOrder: Number(lotOrder),
        jerseyNumber: jerseyNumber.trim() === '' ? null : Number(jerseyNumber),
      })
      setSaved(result)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not save the player.')
    } finally {
      setBusy(false)
    }
  }

  /**
   * Uploaded separately from the form fields: the file goes as multipart to its own
   * endpoint, and a new player has to exist before it has an id to attach a photo to.
   */
  async function handlePhoto(event: ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0]
    if (!file || !saved) return

    setUploading(true)
    setError(null)

    try {
      setSaved(await playersApi.uploadPhoto(saved.id, file))
    } catch (err) {
      setError(errorMessage(err, 'Could not upload that photo.'))
    } finally {
      setUploading(false)
      if (fileRef.current) fileRef.current.value = ''
    }
  }

  return (
    <div
      className="fixed inset-0 z-40 grid place-items-center overflow-y-auto bg-ink-950/70 p-4 backdrop-blur-sm"
      role="dialog"
      aria-modal="true"
      aria-label={player ? `Edit ${player.name}` : 'Add a player'}
    >
      <form onSubmit={handleSubmit} className="glass-strong animate-rise-in w-full max-w-xl space-y-4 p-5">
        <h2 className="text-lg font-bold text-white">
          {player ? `Edit ${player.name}` : 'Add a player'}
        </h2>

        <div className="flex items-center gap-4">
          <PlayerPhoto player={saved ?? { name: name || '?', photoThumbUrl: null, photoUrl: null }} size="lg" />
          <div className="min-w-0 flex-1">
            <label className="field-label" htmlFor="player-photo">
              Photo
            </label>
            <input
              id="player-photo"
              ref={fileRef}
              type="file"
              accept="image/*"
              className="field text-xs"
              disabled={!saved || uploading}
              onChange={(event) => void handlePhoto(event)}
            />
            <p className="mt-1 text-xs text-slate-500">
              {saved
                ? uploading
                  ? 'Uploading…'
                  : 'Resized to a square on the server, so it loads instantly on every screen.'
                : 'Save the player first, then a photo can be attached.'}
            </p>
          </div>
        </div>

        <div className="grid gap-3 sm:grid-cols-2">
          <div className="sm:col-span-2">
            <label className="field-label" htmlFor="player-name">
              Name
            </label>
            <input
              id="player-name"
              className="field"
              value={name}
              onChange={(event) => setName(event.target.value)}
              required
              autoFocus
            />
          </div>

          <div>
            <label className="field-label" htmlFor="player-role">
              Role
            </label>
            <input
              id="player-role"
              className="field"
              value={role}
              onChange={(event) => setRole(event.target.value)}
              placeholder="Batter, Singles specialist…"
            />
          </div>

          <div>
            <label className="field-label" htmlFor="player-gender">
              Gender{genderRequired && ' (required by squad rules)'}
            </label>
            <select
              id="player-gender"
              className="field"
              value={gender}
              onChange={(event) => setGender(event.target.value as Gender)}
            >
              {GENDERS.map((option) => (
                <option key={option.value} value={option.value}>
                  {option.label}
                </option>
              ))}
            </select>
          </div>

          <div>
            <label className="field-label" htmlFor="player-country">
              Country
            </label>
            <input
              id="player-country"
              className="field"
              value={country}
              onChange={(event) => setCountry(event.target.value)}
            />
          </div>

          <div>
            <label className="field-label" htmlFor="player-lot">
              Lot order
            </label>
            <input
              id="player-lot"
              className="field nums"
              type="number"
              min={1}
              step={1}
              value={lotOrder}
              onChange={(event) => setLotOrder(event.target.value)}
              required
            />
          </div>

          <div>
            <label className="field-label" htmlFor="player-jersey">
              Jersey number
            </label>
            <input
              id="player-jersey"
              className="field nums"
              type="number"
              min={0}
              max={999}
              step={1}
              value={jerseyNumber}
              onChange={(event) => setJerseyNumber(event.target.value)}
              placeholder="optional"
            />
            <p className="mt-1 text-xs text-slate-500">
              The number on the shirt. Falls back to the lot order when blank.
            </p>
          </div>

          <div className="sm:col-span-2">
            <label className="field-label" htmlFor="player-base">
              Base price
            </label>
            <input
              id="player-base"
              className="field nums"
              type="number"
              min={0}
              step={1}
              value={basePrice}
              onChange={(event) => setBasePrice(event.target.value)}
              required
            />
          </div>

          {/* One field per attribute the sport declares — no schema change to add a sport. */}
          {skillFields.map((field) => (
            <div key={field}>
              <label className="field-label" htmlFor={`skill-${field}`}>
                {field}
              </label>
              <input
                id={`skill-${field}`}
                className="field"
                value={skills[field] ?? ''}
                onChange={(event) =>
                  setSkills((current) => ({ ...current, [field]: event.target.value }))
                }
              />
            </div>
          ))}
        </div>

        {error && (
          <p
            className="rounded-xl border border-danger-500/40 bg-danger-500/12 px-3 py-2 text-sm text-danger-400"
            role="alert"
          >
            {error}
          </p>
        )}

        <div className="flex justify-end gap-2">
          <button type="button" className="btn" onClick={onClose} disabled={busy}>
            {saved && saved !== player ? 'Done' : 'Cancel'}
          </button>
          <button type="submit" className="btn btn-primary" disabled={busy}>
            {busy ? 'Saving…' : 'Save player'}
          </button>
        </div>
      </form>
    </div>
  )
}
