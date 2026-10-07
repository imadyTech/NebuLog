import { useEffect, useState } from 'react'
import { Link } from 'react-router'
import { FACTS, TIMELINE } from './facts'
import { publicApi, SAMPLE_ROWS, type PublicSummary, type SiteConfig } from './publicApi'
import styles from './LandingPage.module.css'

const CODE = `builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("orders-api"))
    .WithLogging(logging => logging
        .AddNebuLogRedaction()
        .AddNebuLogExporter(o =>
        {
            o.Endpoint = new Uri("https://nebulog.example");
            o.ApiKey = apiKey;
        }));`

export default function LandingPage() {
  const [summary, setSummary] = useState<PublicSummary | null>(null)
  const [config, setConfig] = useState<SiteConfig | null>(null)

  useEffect(() => {
    let cancelled = false

    const load = async () => {
      const next = await publicApi.summary()
      if (!cancelled) {
        setSummary(next)
      }
    }

    void load()
    void publicApi.siteConfig().then((next) => {
      if (!cancelled) {
        setConfig(next)
      }
    })

    // Matches the server's five-second output cache; polling faster would only re-read the cache.
    const timer = window.setInterval(() => void load(), 5000)
    return () => {
      cancelled = true
      window.clearInterval(timer)
    }
  }, [])

  const github = config?.gitHubUrl || 'https://github.com/imadyTech/NebuLog'

  return (
    <div className={styles.page}>
      <header className={styles.topBar}>
        <span className={styles.brand}>NebuLog</span>
        <nav className={styles.topNav}>
          <a href="#how">How it works</a>
          <a href="#highlights">Highlights</a>
          <a href="/scalar/v1">API reference</a>
          <a href={github}>GitHub</a>
          {config?.blogUrl ? <a href={config.blogUrl}>Blog</a> : null}
        </nav>
        <Link to="/login" className={styles.topCta}>
          Try the live demo
        </Link>
      </header>

      <section className={styles.hero}>
        <div className={styles.heroText}>
          <p className={styles.eyebrow}>Open source · .NET 10 · OpenTelemetry</p>
          <h1 className={styles.heroTitle}>Watch your .NET logs arrive as they happen.</h1>
          <p className={styles.heroSub}>
            Your application writes to the standard <code>ILogger</code>. The OpenTelemetry SDK ships every
            record to NebuLog, and a browser console shows it within a tenth of a second — filterable,
            searchable and correlated by trace.
          </p>
          <div className={styles.heroButtons}>
            <Link to="/login" className={styles.primary}>
              Try the live demo
            </Link>
            <a href={github} className={styles.ghost}>
              View on GitHub
            </a>
          </div>
          <p className={styles.heroNote}>
            The demo signs you in as a read-only visitor. The traffic you see is synthetic.
          </p>
        </div>

        <div className={styles.heroConsole} aria-label="Example of the live console">
          <div className={styles.consoleBar}>
            <span>Live stream</span>
            <span className={styles.counter}>
              {summary ? `${summary.ingestedTotal.toLocaleString()} entries · ${summary.serviceCount} services` : '—'}
            </span>
          </div>
          <Sparkline values={summary?.ratePerSecondLast60s ?? []} />
          <table className={styles.consoleTable}>
            <tbody>
              {SAMPLE_ROWS.map((row) => (
                <tr key={`${row.time}-${row.message}`}>
                  <td className={styles.cellTime}>{row.time}</td>
                  <td>
                    <span className={`${styles.level} ${styles[`level${row.level}`]}`}>{row.level}</span>
                  </td>
                  <td className={styles.cellService}>{row.service}</td>
                  <td className={styles.cellMessage}>{row.message}</td>
                </tr>
              ))}
            </tbody>
          </table>
          <p className={styles.consoleNote}>
            Rows are a fixed example; the counter and the rate curve above are this server, live.
          </p>
        </div>
      </section>

      <section id="how" className={styles.section}>
        <h2 className={styles.sectionTitle}>Standards in the application, NebuLog at the edges</h2>
        <p className={styles.sectionLead}>
          Nothing in your code depends on NebuLog. It plugs into the extension points OpenTelemetry
          already defines and speaks OTLP, so any language can reach it.
        </p>
        <div className={styles.flow}>
          <Column
            title="Your application"
            subtitle="Any ASP.NET Core or console process"
            items={['ILogger<T>', 'OpenTelemetry SDK', 'RedactionProcessor', 'NebuLogExporter']}
          />
          <Column
            title="NebuLog server"
            subtitle="Embeddable library or stand-alone host"
            items={['POST /v1/logs (OTLP)', 'Ingest pipeline', 'SignalR hub', 'Identity + API keys']}
            highlight
          />
          <Column
            title="Browser console"
            subtitle="React, TypeScript, one origin"
            items={['Virtual list', 'Filters, search, trace', 'Live summary', 'Operator commands']}
          />
        </div>
      </section>

      <section id="highlights" className={styles.section}>
        <h2 className={styles.sectionTitle}>What is inside</h2>
        <div className={styles.facts}>
          {FACTS.map((fact) => (
            <article key={fact.label} className={styles.fact}>
              <p className={styles.factValue}>{fact.value}</p>
              <h3 className={styles.factLabel}>{fact.label}</h3>
              <p className={styles.factDetail}>{fact.detail}</p>
            </article>
          ))}
        </div>
        <p className={styles.footnote}>
          Figures come from the v2.0.0 evaluation recorded in the repository; each one names the test or
          method it was measured with.
        </p>
      </section>

      <section className={styles.section}>
        <h2 className={styles.sectionTitle}>Four lines to start sending</h2>
        <pre className={styles.code}>
          <code>{CODE}</code>
        </pre>
        <p className={styles.sectionLead}>
          Or point the stock OTLP exporter at <code>/v1/logs</code> — no NebuLog package at all.
        </p>
      </section>

      <section className={styles.section}>
        <h2 className={styles.sectionTitle}>Since 2018</h2>
        <ol className={styles.timeline}>
          {TIMELINE.map((milestone) => (
            <li key={milestone.when}>
              <span className={styles.when}>{milestone.when}</span>
              <span>{milestone.what}</span>
            </li>
          ))}
        </ol>
      </section>

      <footer className={styles.footer}>
        <span>NebuLog · Apache-2.0 · Imady NZ Limited</span>
        <nav className={styles.footerNav}>
          <a href={github}>GitHub</a>
          <a href="/scalar/v1">API reference</a>
          {config?.blogUrl ? <a href={config.blogUrl}>Blog</a> : null}
        </nav>
      </footer>
    </div>
  )
}

function Column({
  title,
  subtitle,
  items,
  highlight = false,
}: {
  title: string
  subtitle: string
  items: string[]
  highlight?: boolean
}) {
  return (
    <div className={highlight ? styles.columnHighlight : styles.column}>
      <h3 className={styles.columnTitle}>{title}</h3>
      <p className={styles.columnSub}>{subtitle}</p>
      <ul className={styles.columnList}>
        {items.map((item) => (
          <li key={item}>{item}</li>
        ))}
      </ul>
    </div>
  )
}

/** A 60-second rate curve, drawn as an SVG polyline. */
function Sparkline({ values }: { values: readonly number[] }) {
  if (values.length === 0) {
    return <div className={styles.sparkEmpty} aria-hidden="true" />
  }

  const max = Math.max(1, ...values)
  const points = values
    .map((value, index) => `${(index / (values.length - 1 || 1)) * 100},${20 - (value / max) * 18}`)
    .join(' ')

  return (
    <svg
      className={styles.spark}
      viewBox="0 0 100 20"
      preserveAspectRatio="none"
      role="img"
      aria-label={`Entries per second over the last minute, peaking at ${max}`}
    >
      <polyline points={points} fill="none" stroke="currentColor" strokeWidth="0.8" vectorEffect="non-scaling-stroke" />
    </svg>
  )
}
