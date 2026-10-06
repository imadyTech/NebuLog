import { useEffect, useRef, useState } from 'react'
import { DEMO_CHANNEL, isDemoMessage, type DemoRequestMessage } from '../../../shared/demoChannel'

/** What the console knows about the visitor's own requests. */
export interface DemoTraceState {
  /** TraceIds the shop window has reported, newest first. */
  traces: readonly DemoRequestMessage[]
}

/**
 * Listens for the shop window on a same-origin BroadcastChannel.
 *
 * Nothing here leaves the browser and nothing reaches the server: the shop and the console are two
 * windows of the same origin, so the correlation is done locally. Messages are validated before use
 * because they drive what the console highlights.
 */
export function useDemoChannel(enabled: boolean, onFocusTrace: (traceId: string) => void): DemoTraceState {
  const [traces, setTraces] = useState<DemoRequestMessage[]>([])
  const channelRef = useRef<BroadcastChannel | null>(null)

  // Held in a ref so re-subscribing does not depend on the caller memoising the handler. Written
  // in an effect rather than during render: a ref is not render state, and assigning it while
  // rendering is what the React compiler warns about.
  const focusRef = useRef(onFocusTrace)
  useEffect(() => {
    focusRef.current = onFocusTrace
  }, [onFocusTrace])

  useEffect(() => {
    if (!enabled || typeof BroadcastChannel === 'undefined') {
      return
    }

    const channel = new BroadcastChannel(DEMO_CHANNEL)
    channelRef.current = channel

    channel.onmessage = (event: MessageEvent<unknown>) => {
      if (!isDemoMessage(event.data)) {
        return
      }

      if (event.data.type === 'request') {
        const message = event.data
        setTraces((current) => [message, ...current.filter((item) => item.traceId !== message.traceId)].slice(0, 20))
        return
      }

      focusRef.current(event.data.traceId)
    }

    return () => {
      channel.close()
      channelRef.current = null
    }
  }, [enabled])

  return { traces }
}
