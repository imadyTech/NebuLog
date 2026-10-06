import { useEffect, useState } from 'react'

/**
 * A clock that ticks on an interval.
 *
 * Reading `Date.now()` during render is impure: the value would be whatever the last render
 * happened to see, and durations derived from it would never update. Ticking it through state
 * makes the dependency explicit and keeps elapsed times moving.
 */
export function useNow(intervalMs = 1000): number {
  const [now, setNow] = useState(() => Date.now())

  useEffect(() => {
    const timer = setInterval(() => setNow(Date.now()), intervalMs)
    return () => clearInterval(timer)
  }, [intervalMs])

  return now
}
