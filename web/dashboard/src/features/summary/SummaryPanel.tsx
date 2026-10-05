import type { LiveSummaryDto } from '../../api/contracts'
import styles from './SummaryPanel.module.css'

const bands = ['Fatal', 'Error', 'Warn', 'Info', 'Debug', 'Trace'] as const

interface SummaryPanelProps {
  summary: LiveSummaryDto | null
  buffered: number
}

/** Ingest totals, the per-second rate over the last minute, and the services seen. */
export default function SummaryPanel({ summary, buffered }: SummaryPanelProps) {
  return (
    <section className={styles.panel}>
      <h2 className={styles.title}>Activity</h2>

      <div className={styles.totals}>
        <Total label="Ingested" value={summary?.totalIngested ?? 0} />
        <Total label="In buffer" value={buffered} />
      </div>

      <Sparkline values={summary?.ratePerSecond ?? []} />

      <ul className={styles.bands}>
        {bands.map((band) => (
          <li key={band} className={styles.band}>
            <span className={`${styles.bandName} ${styles[`level${band}`]}`}>{band}</span>
            <span className={styles.bandCount}>{(summary?.countsByBand[band] ?? 0).toLocaleString()}</span>
          </li>
        ))}
      </ul>

      <h3 className={styles.subtitle}>Services</h3>
      {summary === null || summary.services.length === 0 ? (
        <p className={styles.empty}>None seen yet.</p>
      ) : (
        <ul className={styles.services}>
          {summary.services.map((service) => (
            <li key={service}>{service}</li>
          ))}
        </ul>
      )}
    </section>
  )
}

function Total({ label, value }: { label: string; value: number }) {
  return (
    <div className={styles.total}>
      <span className={styles.totalValue}>{value.toLocaleString()}</span>
      <span className={styles.totalLabel}>{label}</span>
    </div>
  )
}

/**
 * Receive rate over the last 60 seconds, drawn as an inline SVG polyline.
 *
 * Hand-drawn rather than a charting library: it is one path with no axes or interaction, so a
 * dependency would cost more bytes than the whole component.
 */
function Sparkline({ values }: { values: readonly number[] }) {
  const width = 240
  const height = 48
  const peak = Math.max(1, ...values)

  const points =
    values.length < 2
      ? ''
      : values
          .map((value, index) => {
            const x = (index / (values.length - 1)) * width
            const y = height - (value / peak) * (height - 2) - 1
            return `${x.toFixed(1)},${y.toFixed(1)}`
          })
          .join(' ')

  const current = values.length > 0 ? values[values.length - 1]! : 0

  return (
    <figure className={styles.chart}>
      <svg
        viewBox={`0 0 ${width} ${height}`}
        className={styles.spark}
        role="img"
        aria-label={`Entries per second over the last minute, currently ${current}, peak ${peak}`}
        preserveAspectRatio="none"
      >
        {points !== '' && <polyline className={styles.sparkLine} points={points} />}
      </svg>
      <figcaption className={styles.chartCaption}>
        <span>{current.toLocaleString()}/s now</span>
        <span>peak {peak.toLocaleString()}/s</span>
      </figcaption>
    </figure>
  )
}
