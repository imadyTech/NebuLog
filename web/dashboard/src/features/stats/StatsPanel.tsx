import type { StatSnapshot } from '../../api/contracts'
import { resolveStatColor } from './colors'
import styles from './StatsPanel.module.css'

/** Live values producers publish alongside their logs. */
export default function StatsPanel({ stats }: { stats: readonly StatSnapshot[] }) {
  return (
    <section className={styles.panel}>
      <h2 className={styles.title}>Custom stats</h2>

      {stats.length === 0 ? (
        <p className={styles.empty}>No statistics declared.</p>
      ) : (
        <ul className={styles.list}>
          {stats.map((stat) => {
            const color = resolveStatColor(stat.definition.color)

            return (
              <li key={stat.definition.id} className={styles.item}>
                <span className={styles.label}>{stat.definition.title}</span>
                <span className={styles.value} style={color === null ? undefined : { color }}>
                  {stat.latest?.value ?? '—'}
                </span>
              </li>
            )
          })}
        </ul>
      )}
    </section>
  )
}
