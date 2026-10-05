import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  type IRetryPolicy,
  type RetryContext,
} from '@microsoft/signalr'
import { MessagePackHubProtocol } from '@microsoft/signalr-protocol-msgpack'
import type {
  ConnectedClientInfo,
  LiveSummaryDto,
  NebuLogEntry,
  StatDefinition,
  StatUpdate,
} from '../api/contracts'
import { HubRoutes } from '../api/contracts'
import type { LogStore } from '../store/logStore'

export type ConnectionStatus = 'connecting' | 'connected' | 'reconnecting' | 'disconnected'

export interface HubHandlers {
  onLogs: (entries: NebuLogEntry[]) => void
  onSummary: (summary: LiveSummaryDto) => void
  onClients: (clients: ConnectedClientInfo[]) => void
  onStatDefined: (definition: StatDefinition) => void
  onStatUpdated: (update: StatUpdate) => void
  onStatus: (status: ConnectionStatus) => void
}

/** Exponential backoff doubling to a 30-second ceiling, never giving up — as on the server. */
class NeverGiveUpRetryPolicy implements IRetryPolicy {
  nextRetryDelayInMilliseconds(context: RetryContext): number {
    return Math.min(1000 * 2 ** Math.min(context.previousRetryCount, 5), 30_000)
  }
}

/**
 * The dashboard's single hub connection.
 *
 * Incoming batches are staged and flushed into the store once per animation frame: a producer
 * sending ten thousand entries a second still causes at most sixty store mutations a second, so
 * React re-renders at the display's pace rather than the log's.
 */
export class NebuLogHubClient {
  private connection: HubConnection | null = null
  private pending: NebuLogEntry[] = []
  private frame: number | null = null
  private paused = false
  private closed = false

  private readonly store: LogStore
  private readonly handlers: HubHandlers

  constructor(store: LogStore, handlers: HubHandlers) {
    this.store = store
    this.handlers = handlers
  }

  /** While paused, arriving entries are discarded rather than buffered. */
  setPaused(paused: boolean): void {
    this.paused = paused
    if (paused) {
      this.pending = []
    }
  }

  async start(): Promise<void> {
    this.closed = false
    this.handlers.onStatus('connecting')

    const connection = new HubConnectionBuilder()
      .withUrl(HubRoutes.path)
      .withHubProtocol(new MessagePackHubProtocol())
      .withAutomaticReconnect(new NeverGiveUpRetryPolicy())
      .build()

    connection.on(HubRoutes.receiveLogs, (entries: NebuLogEntry[]) => this.stage(entries))
    connection.on(HubRoutes.summaryUpdated, (summary: LiveSummaryDto) => this.handlers.onSummary(summary))
    connection.on(HubRoutes.clientsChanged, (clients: ConnectedClientInfo[]) => this.handlers.onClients(clients))
    connection.on(HubRoutes.statDefined, (definition: StatDefinition) => this.handlers.onStatDefined(definition))
    connection.on(HubRoutes.statUpdated, (update: StatUpdate) => this.handlers.onStatUpdated(update))

    connection.onreconnecting(() => this.handlers.onStatus('reconnecting'))
    connection.onreconnected(() => {
      this.handlers.onStatus('connected')
      void this.fillGap()
    })
    connection.onclose(() => {
      if (!this.closed) {
        this.handlers.onStatus('disconnected')
      }
    })

    this.connection = connection
    await connection.start()
    this.handlers.onStatus('connected')
    await this.fillGap()
  }

  async stop(): Promise<void> {
    this.closed = true
    if (this.frame !== null) {
      cancelAnimationFrame(this.frame)
      this.frame = null
    }

    await this.connection?.stop()
    this.connection = null
    this.handlers.onStatus('disconnected')
  }

  /** Sends a command to one producer. */
  async sendCommand(connectionId: string, name: string, args: Record<string, string>): Promise<void> {
    if (this.connection === null) {
      throw new Error('The hub connection is not open.')
    }

    await this.connection.invoke(HubRoutes.sendCommand, connectionId, {
      name,
      arguments: args,
      issuedBy: '',
      issuedUnixMs: Date.now(),
    })
  }

  /**
   * Replays anything the server buffered while the connection was down, starting just after the
   * last id already held. The store also drops ids it has seen, so an overlap cannot duplicate rows.
   */
  private async fillGap(): Promise<void> {
    if (this.connection === null || this.connection.state !== HubConnectionState.Connected) {
      return
    }

    const afterId = this.store.lastId
    const replayed: NebuLogEntry[] = []

    try {
      const stream = this.connection.stream<NebuLogEntry>(HubRoutes.streamHistory, {
        afterId: afterId === 0 ? null : afterId,
        minSeverity: null,
        service: null,
        limit: 10_000,
      })

      await new Promise<void>((resolve, reject) => {
        stream.subscribe({
          next: (entry) => replayed.push(entry),
          complete: () => resolve(),
          error: (error) => reject(error instanceof Error ? error : new Error(String(error))),
        })
      })
    } catch {
      // History is a convenience: a failure here must not take the live stream down with it.
      return
    }

    if (replayed.length > 0) {
      this.store.append(replayed)
    }
  }

  private stage(entries: NebuLogEntry[]): void {
    if (this.paused || entries.length === 0) {
      return
    }

    this.pending.push(...entries)
    this.scheduleFlush()
  }

  private scheduleFlush(): void {
    if (this.frame !== null) {
      return
    }

    this.frame = requestAnimationFrame(() => {
      this.frame = null
      const batch = this.pending
      this.pending = []

      if (batch.length > 0) {
        this.store.append(batch)
        this.handlers.onLogs(batch)
      }
    })
  }
}
