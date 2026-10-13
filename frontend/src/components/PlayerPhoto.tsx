import { useEffect } from 'react'
import { hueFor, initials } from '../lib/format'
import type { Player } from '../types'

const DIMENSIONS = {
  sm: { px: 32, className: 'size-8 text-xs' },
  md: { px: 44, className: 'size-11 text-sm' },
  lg: { px: 80, className: 'size-20 text-2xl' },
  xl: { px: 144, className: 'size-36 text-4xl' },
} as const

type PhotoSize = keyof typeof DIMENSIONS

/**
 * A player's face, or a coloured monogram when there is no photo.
 *
 * Two things keep this instant. The box is sized in CSS before the image exists, so a
 * photo arriving never reflows the page around it. And the source is one of two
 * server-generated squares — a 96px thumbnail or a 480px card crop — served from an
 * immutable URL, so the browser reuses it from cache for the rest of the session
 * instead of re-downloading a full-size upload.
 */
export function PlayerPhoto({
  player,
  size = 'md',
  className = '',
  zoom = false,
}: {
  player: Pick<Player, 'name' | 'photoThumbUrl' | 'photoUrl'>
  size?: PhotoSize
  className?: string
  /**
   * Expand to the full picture on hover or keyboard focus. For lists, where the thumbnail
   * is too small to recognise anyone by.
   */
  zoom?: boolean
}) {
  const { px, className: sizeClass } = DIMENSIONS[size]

  // The thumbnail is enough below ~64px and saves a much larger download.
  const source = px <= 64 ? player.photoThumbUrl : (player.photoUrl ?? player.photoThumbUrl)
  const hue = hueFor(player.name)

  const face = source ? (
    <img
      src={source}
      alt=""
      width={px}
      height={px}
      loading="eager"
      decoding="async"
      className={`${sizeClass} shrink-0 rounded-full border border-white/20 bg-white/6 object-cover ${className}`}
    />
  ) : (
    <span
      aria-hidden
      className={`${sizeClass} grid shrink-0 place-items-center rounded-full border border-white/20 font-bold text-white ${className}`}
      style={{
        background: `linear-gradient(140deg, hsl(${hue} 72% 52%), hsl(${(hue + 48) % 360} 68% 40%))`,
      }}
    >
      {initials(player.name)}
    </span>
  )

  // Nothing to enlarge to when there is no photo — a bigger monogram tells you nothing.
  if (!zoom || !player.photoUrl) return face

  return (
    <span className="photo-zoom" tabIndex={0} aria-label={player.name}>
      {face}
      <span className="photo-zoom-panel glass-strong" aria-hidden>
        <img src={player.photoUrl} alt="" width={192} height={192} decoding="async" />
        <span className="photo-zoom-name">{player.name}</span>
      </span>
    </span>
  )
}

/**
 * Warms the browser cache for lots that have not been called yet, so the photo is
 * already decoded by the time the auctioneer brings the player up.
 */
export function usePhotoPreload(players: Pick<Player, 'photoUrl' | 'photoThumbUrl'>[]) {
  // Joined into one string so the effect only re-runs when the set of URLs actually changes.
  const urls = players
    .map((player) => player.photoUrl ?? player.photoThumbUrl)
    .filter((url): url is string => Boolean(url))

  const key = urls.join('|')

  useEffect(() => {
    if (!key) return

    // Plain Image objects rather than <link rel=preload>: no DOM churn, and the browser
    // treats the result as a normal cache hit when the real <img> mounts.
    const images = key.split('|').map((url) => {
      const image = new Image()
      image.decoding = 'async'
      image.src = url
      return image
    })

    return () => {
      // Dropping src lets an in-flight request be abandoned if the view unmounts early.
      for (const image of images) image.src = ''
    }
  }, [key])
}
