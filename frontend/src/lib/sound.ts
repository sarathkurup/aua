/**
 * Auction room sound cues.
 *
 * Synthesised with the Web Audio API rather than played from files: a cue must land on the
 * same beat as the number it confirms, and a fetched asset can arrive late or not at all.
 * Nothing here touches the network.
 *
 * Browsers refuse to start audio until the page has had a real user gesture, and a spectator
 * arrives without clicking anything — so the context stays closed until {@link unlockSound}
 * is called from an actual click, and every cue is a no-op before that.
 */

export type SoundCue = 'bid' | 'sold' | 'unsold'

let context: AudioContext | null = null
let enabled = false

/** Mirrors the user's preference. Cues are silent while false. */
export function setSoundEnabled(value: boolean): void {
  enabled = value
}

export function isSoundEnabled(): boolean {
  return enabled
}

/**
 * Opens the audio context. Must be called from a user gesture — a click handler, not an
 * effect — or the browser leaves it suspended.
 */
export async function unlockSound(): Promise<boolean> {
  try {
    const Ctor = window.AudioContext ?? (window as unknown as { webkitAudioContext?: typeof AudioContext }).webkitAudioContext
    if (!Ctor) return false

    context ??= new Ctor()
    if (context.state === 'suspended') await context.resume()
    return context.state === 'running'
  } catch {
    // Audio is a nicety; a browser that refuses it should not break the room.
    return false
  }
}

export function closeSound(): void {
  void context?.close()
  context = null
}

interface ToneSpec {
  freq: number
  /** Seconds from now. */
  at: number
  duration: number
  type?: OscillatorType
  peak?: number
  /** Slides to this frequency across the tone. Gives the bid blip its lift. */
  glideTo?: number
}

function tone(ctx: AudioContext, spec: ToneSpec): void {
  const start = ctx.currentTime + spec.at
  const end = start + spec.duration

  const osc = ctx.createOscillator()
  osc.type = spec.type ?? 'sine'
  osc.frequency.setValueAtTime(spec.freq, start)
  if (spec.glideTo !== undefined) {
    osc.frequency.exponentialRampToValueAtTime(spec.glideTo, end)
  }

  // Ramped rather than switched: a gain that jumps to full produces an audible click.
  const gain = ctx.createGain()
  gain.gain.setValueAtTime(0.0001, start)
  gain.gain.exponentialRampToValueAtTime(spec.peak ?? 0.16, start + 0.012)
  gain.gain.exponentialRampToValueAtTime(0.0001, end)

  osc.connect(gain)
  gain.connect(ctx.destination)
  osc.start(start)
  osc.stop(end + 0.03)
}

/**
 * @param intensity 0–1, how far the bid has climbed above the base price. Raises the pitch,
 * so a room can hear a lot heating up without reading the number.
 */
export function playCue(cue: SoundCue, intensity = 0): void {
  if (!enabled || !context || context.state !== 'running') return

  const lift = Math.min(1, Math.max(0, intensity))

  switch (cue) {
    case 'bid': {
      const base = 620 + lift * 380
      tone(context, { freq: base, glideTo: base * 1.28, at: 0, duration: 0.11, peak: 0.14 })
      break
    }

    case 'sold': {
      // Rising major triad — reads as a resolution, which is what a hammer is.
      tone(context, { freq: 523.25, at: 0, duration: 0.13, peak: 0.15 })
      tone(context, { freq: 659.25, at: 0.1, duration: 0.13, peak: 0.15 })
      tone(context, { freq: 783.99, at: 0.2, duration: 0.34, peak: 0.18 })
      break
    }

    case 'unsold': {
      tone(context, { freq: 190, at: 0, duration: 0.16, type: 'triangle', peak: 0.13 })
      tone(context, { freq: 150, at: 0.14, duration: 0.24, type: 'triangle', peak: 0.13 })
      break
    }
  }
}
