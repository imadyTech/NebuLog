import { useCallback, useState } from 'react'
import { useNavigate } from 'react-router'
import { SCENARIOS, type Scenario, type ShopApi } from '../../../shared/scenarios'
import { launchScenario } from './runScenario'
import styles from './DemoPage.module.css'

/** The guided demos: eight scenarios, each one click away from a shop window and a filtered console. */
export default function DemoPage() {
  const navigate = useNavigate()
  const [api, setApi] = useState<ShopApi>('minimal')

  const run = useCallback(
    (scenario: Scenario) => {
      // Synchronous, in the click handler: see launchScenario. Nothing may be awaited before it.
      const launch = launchScenario(scenario, api)
      const url = launch.shopOpened || scenario.action.kind === 'none'
        ? launch.consoleUrl
        : `${launch.consoleUrl}&popup=blocked`

      void navigate(url)
    },
    [api, navigate],
  )

  return (
    <div className={styles.page}>
      <header className={styles.intro}>
        <h1 className={styles.title}>Guided demos</h1>
        <p className={styles.lead}>
          Each scenario opens a small shop application in a new window and switches this tab to the live
          console. Do something in the shop; watch the matching logs arrive here, tagged with the same trace.
        </p>
        <ol className={styles.steps}>
          <li>
            <strong>Choose a scenario.</strong> Each card says what to do and what to look for.
          </li>
          <li>
            <strong>Two windows open.</strong> The shop opens beside you; this tab becomes the live console,
            already filtered.
          </li>
          <li>
            <strong>Act, then inspect.</strong> Every shop request shows its TraceId. The console highlights
            the rows that trace as they arrive.
          </li>
        </ol>
        <div className={styles.apiRow}>
          <span className={styles.apiLabel}>Shop backend</span>
          <div className={styles.apiToggle} role="group" aria-label="Backend implementation">
            {(['minimal', 'mvc'] as const).map((candidate) => (
              <button
                key={candidate}
                type="button"
                aria-pressed={api === candidate}
                className={styles.apiButton}
                onClick={() => setApi(candidate)}
              >
                {candidate === 'minimal' ? 'Minimal API' : 'MVC'}
              </button>
            ))}
          </div>
          <span className={styles.apiHint}>
            Both serve the same routes. MVC logs considerably more of its own pipeline.
          </span>
        </div>
      </header>

      <ul className={styles.cards}>
        {SCENARIOS.map((scenario) => (
          <li key={scenario.id} className={styles.card}>
            <div className={styles.cardHead}>
              <span className={styles.number}>{scenario.number}</span>
              <span className={styles.duration}>{scenario.duration}</span>
            </div>
            <h2 className={styles.cardTitle}>{scenario.title}</h2>
            <p className={styles.line}>
              <span className={styles.lineLabel}>You will</span>
              {scenario.youWill}
            </p>
            <p className={styles.line}>
              <span className={styles.lineLabel}>Watch for</span>
              {scenario.watchFor}
            </p>
            <ul className={styles.tags}>
              {scenario.tags.map((tag) => (
                <li key={tag} className={styles.tag}>
                  {tag}
                </li>
              ))}
            </ul>
            {scenario.link ? (
              <a className={styles.secondary} href={scenario.link.href}>
                {scenario.link.label}
              </a>
            ) : (
              <button type="button" className={styles.primary} onClick={() => run(scenario)}>
                Run scenario
              </button>
            )}
          </li>
        ))}
      </ul>
    </div>
  )
}
