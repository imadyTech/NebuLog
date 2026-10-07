import type { Scenario } from '../../../shared/scenarios'
import { shopUrlFor } from './runScenario'
import styles from './ScenarioBanner.module.css'

interface Props {
  scenario: Scenario
  /** True when the shop popup was blocked, so the banner offers another way in. */
  popupBlocked: boolean
  /** True when the console is already narrowed to one trace. */
  traceFiltered: boolean
  /** The visitor's most recent trace, if the shop has reported one. */
  latestTrace: string | null
  onShowOnlyMyTrace: () => void
  onDismiss: () => void
}

/**
 * The strip that tells a visitor what they are looking at and what to do next.
 *
 * `role="status"` rather than `alert`: it is informative, and an assertive announcement would
 * interrupt a screen-reader user mid-sentence every time a scenario starts.
 */
export default function ScenarioBanner({
  scenario,
  popupBlocked,
  traceFiltered,
  latestTrace,
  onShowOnlyMyTrace,
  onDismiss,
}: Props) {
  return (
    <section className={styles.banner} role="status">
      <span className={styles.number}>{scenario.number}</span>

      <div className={styles.text}>
        <p className={styles.title}>{scenario.title}</p>
        <p className={styles.hint}>{popupBlocked ? 'The shop window was blocked by the browser. Open it manually to run this scenario.' : scenario.watchFor}</p>
      </div>

      <div className={styles.actions}>
        {latestTrace && !traceFiltered ? (
          <button type="button" className={styles.action} onClick={onShowOnlyMyTrace}>
            Show only my trace
          </button>
        ) : null}
        {scenario.action.kind !== 'none' ? (
          <a
            className={styles.action}
            href={shopUrlFor(scenario, 'minimal')}
            target="nebushop"
            rel="opener"
          >
            {popupBlocked ? 'Open shop' : 'Reopen shop window'}
          </a>
        ) : null}
        <button type="button" className={styles.dismiss} onClick={onDismiss} aria-label="Dismiss scenario hint">
          ×
        </button>
      </div>
    </section>
  )
}
