import { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react'
import type { ReactNode } from 'react'
import { setSoundEnabled, unlockSound } from '../lib/sound'

/** How a completed sale is celebrated. */
export type SaleAnimation = 'flight' | 'overlay' | 'none'

const SOUND_KEY = 'auction.sound'
const ANIMATION_KEY = 'auction.saleAnimation'

interface PreferencesValue {
  /** True only once the user has turned sound on *and* the browser let us open audio. */
  sound: boolean
  /**
   * Turns sound on or off. Must be called from a click: opening an AudioContext from
   * anywhere else leaves it suspended. Returns the state actually achieved.
   */
  setSound: (on: boolean) => Promise<boolean>
  saleAnimation: SaleAnimation
  setSaleAnimation: (value: SaleAnimation) => void
}

const PreferencesContext = createContext<PreferencesValue | null>(null)

function storedAnimation(): SaleAnimation {
  const saved = localStorage.getItem(ANIMATION_KEY)
  return saved === 'flight' || saved === 'overlay' || saved === 'none' ? saved : 'flight'
}

/**
 * Room preferences that belong to the device rather than the account — spectators have no
 * account at all, so these live in localStorage next to the theme.
 */
export function PreferencesProvider({ children }: { children: ReactNode }) {
  const [sound, setSoundState] = useState(false)
  const [saleAnimation, setAnimationState] = useState<SaleAnimation>(() => storedAnimation())

  // The stored preference is only an intent. Audio still cannot start without a gesture, so
  // a returning user stays silent until they click the toggle again — which the button says.
  useEffect(() => {
    setSoundEnabled(false)
  }, [])

  const setSound = useCallback(async (on: boolean) => {
    if (!on) {
      setSoundEnabled(false)
      setSoundState(false)
      localStorage.setItem(SOUND_KEY, 'off')
      return false
    }

    const unlocked = await unlockSound()
    setSoundEnabled(unlocked)
    setSoundState(unlocked)
    localStorage.setItem(SOUND_KEY, unlocked ? 'on' : 'off')
    return unlocked
  }, [])

  const setSaleAnimation = useCallback((value: SaleAnimation) => {
    setAnimationState(value)
    localStorage.setItem(ANIMATION_KEY, value)
  }, [])

  const value = useMemo(
    () => ({ sound, setSound, saleAnimation, setSaleAnimation }),
    [sound, setSound, saleAnimation, setSaleAnimation],
  )

  return <PreferencesContext value={value}>{children}</PreferencesContext>
}

export function usePreferences(): PreferencesValue {
  const context = useContext(PreferencesContext)
  if (!context) throw new Error('usePreferences must be used inside <PreferencesProvider>')
  return context
}

/** True when the user previously had sound on, so the UI can nudge them to re-enable it. */
export function soundWasEnabled(): boolean {
  return localStorage.getItem(SOUND_KEY) === 'on'
}
