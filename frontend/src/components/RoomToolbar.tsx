import { useState } from 'react'
import { Link } from 'react-router'
import type { Auction } from '../types'
import { usePreferences, soundWasEnabled } from '../settings/PreferencesContext'
import { share, roomUrl } from '../lib/share'
import { useToast } from './Toasts'

/**
 * Room controls that belong to the viewer rather than the auction: sound, focus mode,
 * sharing, and a link across to the results.
 *
 * The sound button carries the weight here. Browsers will not start audio until the page has
 * had a real click, and a spectator arrives without making one — so it cannot be defaulted
 * on, and the label has to make clear that turning it on is a deliberate act.
 */
export function RoomToolbar({
  auction,
  viewerCount,
  focusMode,
  onToggleFocus,
}: {
  auction: Auction
  viewerCount?: number
  focusMode: boolean
  onToggleFocus: () => void
}) {
  const { sound, setSound } = usePreferences()
  const { push } = useToast()
  const [busy, setBusy] = useState(false)

  // Someone who had sound on last visit gets a nudge rather than silence they did not ask for.
  const wasOn = !sound && soundWasEnabled()

  async function toggleSound() {
    setBusy(true)
    const enabled = await setSound(!sound)
    setBusy(false)

    if (!sound && !enabled) {
      push('This browser refused to start audio. Try clicking the page first.', 'warn')
    }
  }

  async function shareRoom() {
    const outcome = await share({
      title: auction.name,
      text: `Watch ${auction.name} live`,
      url: roomUrl(auction.publicId),
    })

    if (outcome === 'copied') push('Link copied.', 'success')
    if (outcome === 'failed') push('Could not share. Copy the link from the address bar.', 'warn')
  }

  return (
    <div className="mt-2.5 flex flex-wrap items-center gap-2 border-t border-white/8 pt-2.5">
      {viewerCount !== undefined && viewerCount > 0 && (
        <span
          className="pill pill-muted"
          title={`${viewerCount} ${viewerCount === 1 ? 'person is' : 'people are'} watching`}
        >
          <span aria-hidden>👁</span>
          <span className="nums">{viewerCount}</span>
        </span>
      )}

      <button
        type="button"
        className={`btn btn-sm ${sound ? 'btn-live' : wasOn ? 'btn-warn' : ''}`}
        onClick={() => void toggleSound()}
        disabled={busy}
        aria-pressed={sound}
        title={
          sound
            ? 'Sound on — a tone on every bid and sale'
            : 'Sound off. Browsers need a click before audio can start.'
        }
      >
        <span aria-hidden>{sound ? '🔊' : '🔇'}</span>
        {sound ? 'Sound on' : wasOn ? 'Re-enable sound' : 'Enable sound'}
      </button>

      <button
        type="button"
        className={`btn btn-sm ${focusMode ? 'btn-primary' : ''}`}
        onClick={onToggleFocus}
        aria-pressed={focusMode}
        title="Hide everything but the lot, the feed and the squads — for a projector or a screen share"
      >
        {focusMode ? 'Show all' : 'Focus'}
      </button>

      <div className="ml-auto flex items-center gap-2">
        <Link to={`/results/${auction.publicId}`} className="btn btn-sm">
          Results
        </Link>
        <button type="button" className="btn btn-sm" onClick={() => void shareRoom()}>
          Share
        </button>
      </div>
    </div>
  )
}
