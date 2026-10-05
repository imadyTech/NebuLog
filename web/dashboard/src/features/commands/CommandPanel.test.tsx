import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import type { ConnectedClientInfo } from '../../api/contracts'
import CommandPanel from './CommandPanel'

const producer: ConnectedClientInfo = {
  connectionId: 'abcdef0123456789',
  kind: 'producer',
  serviceName: 'orders',
  serviceInstanceId: 'orders-1',
  connectedUnixMs: Date.now() - 5000,
}

describe('CommandPanel', () => {
  it('invites the operator to connect a producer when there are none', () => {
    render(<CommandPanel producers={[]} selectedId={null} onSelect={vi.fn()} send={vi.fn()} />)

    expect(screen.getByText('Connect a producer to send commands.')).toBeInTheDocument()
  })

  it('sends ping to the chosen producer', async () => {
    const send = vi.fn().mockResolvedValue(undefined)
    render(
      <CommandPanel producers={[producer]} selectedId={producer.connectionId} onSelect={vi.fn()} send={send} />,
    )

    await userEvent.click(screen.getByRole('button', { name: 'ping' }))

    expect(send).toHaveBeenCalledWith(producer.connectionId, 'ping', {})
    expect(await screen.findByRole('status')).toHaveTextContent('Sent ping to orders.')
  })

  it('sends set-min-level with the chosen level', async () => {
    const send = vi.fn().mockResolvedValue(undefined)
    render(
      <CommandPanel producers={[producer]} selectedId={producer.connectionId} onSelect={vi.fn()} send={send} />,
    )

    await userEvent.selectOptions(screen.getByLabelText('Minimum level'), '17')
    await userEvent.click(screen.getByRole('button', { name: 'set-min-level' }))

    expect(send).toHaveBeenCalledWith(producer.connectionId, 'set-min-level', { level: '17' })
  })

  it('reports a refused command', async () => {
    const send = vi.fn().mockRejectedValue(new Error('Unauthorized'))
    render(
      <CommandPanel producers={[producer]} selectedId={producer.connectionId} onSelect={vi.fn()} send={send} />,
    )

    await userEvent.click(screen.getByRole('button', { name: 'ping' }))

    expect(await screen.findByRole('status')).toHaveTextContent('Unauthorized')
  })

  it('will not send without a target', async () => {
    const send = vi.fn()
    render(<CommandPanel producers={[producer]} selectedId={null} onSelect={vi.fn()} send={send} />)

    expect(screen.getByRole('button', { name: 'ping' })).toBeDisabled()
    expect(send).not.toHaveBeenCalled()
  })
})
