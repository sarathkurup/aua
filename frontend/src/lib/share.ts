/**
 * Sharing a room, a squad or a signing.
 *
 * Uses the native share sheet where the device has one — on a phone that is the difference
 * between "send this to the group" being one tap or four. Everywhere else it falls back to
 * the clipboard, and if that is refused too (it is, on insecure origins) the caller is told
 * so it can show the link to copy by hand.
 */

export type ShareOutcome = 'shared' | 'copied' | 'failed'

export interface SharePayload {
  title: string
  text: string
  url: string
}

export async function share(payload: SharePayload): Promise<ShareOutcome> {
  if (navigator.share) {
    try {
      await navigator.share(payload)
      return 'shared'
    } catch (error) {
      // A cancelled share sheet is a normal outcome, not a failure to fall back from.
      if (error instanceof DOMException && error.name === 'AbortError') return 'shared'
    }
  }

  try {
    await navigator.clipboard.writeText(`${payload.text}\n${payload.url}`)
    return 'copied'
  } catch {
    return 'failed'
  }
}

/** The room's public address — the only one a spectator is ever given. */
export function roomUrl(publicId: string): string {
  return `${window.location.origin}/watch/${publicId}`
}

export function resultsUrl(publicId: string): string {
  return `${window.location.origin}/results/${publicId}`
}

/** Deep link to one squad on the results page. */
export function teamUrl(publicId: string, teamId: number): string {
  return `${resultsUrl(publicId)}#team-${teamId}`
}
