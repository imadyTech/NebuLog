/**
 * The figures the landing page quotes.
 *
 * Kept in one file, with the source of each number written next to it, because a marketing page
 * that drifts from the measurements is worse than one that quotes nothing. Every value here comes
 * from the v2.0.0 evaluation in docs/journey/journey-log.md ("评估：v1 → v2 量化对比"), measured on
 * 2026-10-05. When the evaluation is re-run, this file is what changes.
 */

export interface Fact {
  /** The headline figure. */
  value: string
  /** What it measures. */
  label: string
  /** One sentence of context. */
  detail: string
}

export const FACTS: readonly Fact[] = [
  {
    value: 'OTLP',
    label: 'OpenTelemetry-native',
    detail:
      'Receives OTLP/HTTP in protobuf and JSON. A compatibility test drives the stock exporter end to end with nothing but a URL.',
  },
  {
    value: '10,077/s',
    label: 'Sustained ingest',
    detail:
      'Entries per second for five seconds with a zero rejection rate (ThroughputSmokeTests, 2026-10-05).',
  },
  {
    value: '109 ms',
    label: 'Batched fan-out',
    detail: 'Median producer-to-browser latency on an idle server; p95 115 ms (BroadcastLatencyTests).',
  },
  {
    value: '~50 rows',
    label: 'A console that stays smooth',
    detail:
      'In the DOM for a 100,000-entry buffer: the ring lives outside React, commits are batched per frame, and the table is virtualised.',
  },
  {
    value: '4 roles',
    label: 'Two kinds of identity',
    detail: 'Cookie sessions for people, hashed API keys for programs. Viewer, Operator, Admin and Producer.',
  },
  {
    value: 'read-only',
    label: 'Production hygiene',
    detail:
      'Containers run with a read-only root filesystem and one writable volume. Images carry immutable tags, never latest.',
  },
]

/** The project's own history, for the "Since 2018" timeline. */
export interface Milestone {
  when: string
  what: string
}

export const TIMELINE: readonly Milestone[] = [
  { when: '2018-12', what: 'First commit, as MyLogger: a WinForms viewer for a logging experiment.' },
  { when: '2019-08', what: 'Renamed NebuLog; the server and dashboard split apart.' },
  { when: '2020-09', what: 'Custom live statistics, and a Unity collector.' },
  { when: '2022-05', what: 'Unity 3D monitoring.' },
  { when: '2022-10', what: 'Last v1 release. .NET 6, 57 dependency advisories, no tests.' },
  { when: '2026-10', what: 'v2: a rewrite on .NET 10 around OpenTelemetry.' },
]
