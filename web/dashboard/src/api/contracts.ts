// Mirrors NebuLog.Contracts. Times are Unix epoch milliseconds throughout, matching the server:
// JSON, MessagePack and JavaScript then agree on both precision and format.

export type IngestSource = 'Otlp' | 'SignalR'

export interface ExceptionInfo {
  type: string
  message: string
  stackTrace: string | null
}

export interface NebuLogEntry {
  id: number
  timestampUnixMs: number
  observedUnixMs: number
  severityNumber: number
  severityText: string | null
  body: string
  serviceName: string
  serviceInstanceId: string | null
  scopeName: string
  traceId: string | null
  spanId: string | null
  eventName: string | null
  attributes: Record<string, string>
  exception: ExceptionInfo | null
  source: IngestSource
}

export interface LiveSummaryDto {
  totalIngested: number
  countsByBand: Record<string, number>
  ratePerSecond: number[]
  services: string[]
  bufferedCount: number
  timestampUnixMs: number
}

export interface StatDefinition {
  id: string
  title: string
  color: string | null
}

export interface StatUpdate {
  id: string
  value: string
  timestampUnixMs: number
}

export interface StatSnapshot {
  definition: StatDefinition
  latest: StatUpdate | null
}

export interface ConnectedClientInfo {
  connectionId: string
  kind: 'producer' | 'viewer'
  serviceName: string | null
  serviceInstanceId: string | null
  connectedUnixMs: number
}

export interface NebuLogCommand {
  name: string
  arguments: Record<string, string>
  issuedBy: string
  issuedUnixMs: number
}

export interface HistoryQuery {
  afterId?: number | null
  minSeverity?: number | null
  service?: string | null
  limit: number
}

export interface CurrentUser {
  email: string
  roles: string[]
}

export interface ServerInfo {
  version: string
  startedUnixMs: number
  uptimeSeconds: number
  bufferCapacity: number
  bufferedCount: number
}

export interface ApiKeyDto {
  id: string
  name: string
  prefix: string
  serviceName: string | null
  createdAt: string
  revokedAt: string | null
  lastUsedAt: string | null
  createdBy: string
}

export interface CreatedApiKeyDto {
  id: string
  name: string
  prefix: string
  serviceName: string | null
  createdAt: string
  apiKey: string
}

/** Server-to-client hub method names; must match NebuLog.Contracts.HubRoutes. */
export const HubRoutes = {
  path: '/hubs/nebulog',
  publishLogs: 'PublishLogs',
  streamHistory: 'StreamHistory',
  getStats: 'GetStats',
  sendCommand: 'SendCommand',
  receiveLogs: 'ReceiveLogs',
  statDefined: 'StatDefined',
  statUpdated: 'StatUpdated',
  clientsChanged: 'ClientsChanged',
  summaryUpdated: 'SummaryUpdated',
  receiveCommand: 'ReceiveCommand',
} as const

export const Roles = {
  viewer: 'Viewer',
  operator: 'Operator',
  admin: 'Admin',
  producer: 'Producer',
} as const
