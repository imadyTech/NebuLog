import { useVirtualizer } from '@tanstack/react-virtual'
import { memo, useCallback, useEffect, useRef, useState } from 'react'
import type { NebuLogEntry } from '../../api/contracts'
import { severityBand, severityShortName } from '../../api/severity'
import type { LogStore } from '../../store/logStore'
import styles from './LogTable.module.css'

/** Fixed row height keeps the virtualiser's offset maths O(1). */
const ROW_HEIGHT = 26

interface LogTableProps {
  store: LogStore
  /** Bumped by the store on every mutation; drives re-render without copying rows into state. */
  version: number
  selectedId: number | null
  onSelect: (entry: NebuLogEntry) => void
}

/**
 * Virtualised log table.
 *
 * Only the rows in view exist in the DOM. Rows read straight out of the ring buffer by index, so
 * nothing is copied per render — which is what lets a hundred thousand buffered entries scroll at
 * display rate.
 */
export default function LogTable({ store, version, selectedId, onSelect }: LogTableProps) {
  const scrollRef = useRef<HTMLDivElement>(null)
  const [following, setFollowing] = useState(true)
  const [unseen, setUnseen] = useState(0)
  const lastCount = useRef(0)

  const count = store.filteredCount

  const virtualizer = useVirtualizer({
    count,
    getScrollElement: () => scrollRef.current,
    estimateSize: () => ROW_HEIGHT,
    overscan: 12,
  })

  const scrollToEnd = useCallback(() => {
    if (count > 0) {
      virtualizer.scrollToIndex(count - 1, { align: 'end' })
    }

    setUnseen(0)
    setFollowing(true)
  }, [count, virtualizer])

  // Follow mode: stay pinned to the newest row until the reader scrolls away, then count what
  // they have not seen rather than yanking the viewport out from under them.
  useEffect(() => {
    const grew = count - lastCount.current
    lastCount.current = count

    if (grew <= 0) {
      return
    }

    if (following) {
      virtualizer.scrollToIndex(count - 1, { align: 'end' })
    } else {
      setUnseen((current) => current + grew)
    }
  }, [count, following, version, virtualizer])

  function onScroll() {
    const element = scrollRef.current
    if (element === null) {
      return
    }

    const atBottom = element.scrollHeight - element.scrollTop - element.clientHeight < ROW_HEIGHT * 2
    setFollowing(atBottom)
    if (atBottom) {
      setUnseen(0)
    }
  }

  return (
    // role="grid" with gridcell children: a role="row" whose children carry no cell role is
    // incomplete ARIA, and a screen reader cannot then walk the columns.
    <div className={styles.wrapper} role="grid" aria-label="Log entries">
      <div className={styles.header} role="row">
        <span className={styles.time} role="columnheader">Time</span>
        <span className={styles.level} role="columnheader">Level</span>
        <span className={styles.service} role="columnheader">Service</span>
        <span className={styles.scope} role="columnheader">Category</span>
        <span className={styles.message} role="columnheader">Message</span>
      </div>

      <div className={styles.scroller} ref={scrollRef} onScroll={onScroll} role="rowgroup" tabIndex={0}>
        <div className={styles.canvas} style={{ height: `${virtualizer.getTotalSize()}px` }}>
          {virtualizer.getVirtualItems().map((item) => {
            const entry = store.filteredAt(item.index)
            if (entry === undefined) {
              return null
            }

            return (
              <LogRow
                key={entry.id}
                entry={entry}
                top={item.start}
                selected={entry.id === selectedId}
                onSelect={onSelect}
              />
            )
          })}
        </div>
      </div>

      {unseen > 0 && (
        <button type="button" className={styles.jump} onClick={scrollToEnd}>
          {unseen.toLocaleString()} new {unseen === 1 ? 'entry' : 'entries'} ↓
        </button>
      )}
    </div>
  )
}

interface LogRowProps {
  entry: NebuLogEntry
  top: number
  selected: boolean
  onSelect: (entry: NebuLogEntry) => void
}

/**
 * One row. Memoised on identity, so scrolling re-renders only the rows entering the viewport.
 *
 * Every field is rendered as text — React escapes it. v1 injected log bodies as HTML, which made
 * any producer able to script the dashboard; nothing here injects raw HTML by any route.
 */
const LogRow = memo(function LogRow({ entry, top, selected, onSelect }: LogRowProps) {
  const band = severityBand(entry.severityNumber)

  return (
    <div
      className={`${styles.row} ${selected ? styles.selected : ''}`}
      style={{ transform: `translateY(${top}px)` }}
      role="row"
      tabIndex={0}
      onClick={() => onSelect(entry)}
      onKeyDown={(event) => {
        if (event.key === 'Enter' || event.key === ' ') {
          event.preventDefault()
          onSelect(entry)
        }
      }}
    >
      <span className={styles.time} role="gridcell">{formatTime(entry.timestampUnixMs)}</span>
      <span className={`${styles.level} ${styles[`level${band}`]}`} role="gridcell">
        {severityShortName(entry.severityNumber)}
      </span>
      <span className={styles.service} role="gridcell">{entry.serviceName}</span>
      <span className={styles.scope} role="gridcell">{entry.scopeName}</span>
      <span className={styles.message} role="gridcell">{entry.body}</span>
    </div>
  )
})

const timeFormat = new Intl.DateTimeFormat(undefined, {
  hour: '2-digit',
  minute: '2-digit',
  second: '2-digit',
  hour12: false,
})

function formatTime(unixMs: number): string {
  const millis = unixMs % 1000
  return `${timeFormat.format(unixMs)}.${millis.toString().padStart(3, '0')}`
}
