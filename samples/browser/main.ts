/**
 * Minimal browser producer: builds an OTLP/JSON ExportLogsServiceRequest by hand and POSTs it.
 *
 * OTLP/JSON is not protobuf's canonical JSON mapping — trace and span ids are hex rather than
 * base64, and 64-bit integers travel as strings — which is exactly what the server's hand-written
 * decoder expects. There is no SDK here on purpose: the point is to show how little it takes.
 */
const byId = <T extends HTMLElement>(id: string): T => document.getElementById(id) as T

const keyInput = byId<HTMLInputElement>('key')
const messageInput = byId<HTMLInputElement>('message')
const severitySelect = byId<HTMLSelectElement>('severity')
const sendButton = byId<HTMLButtonElement>('send')
const result = byId<HTMLOutputElement>('result')
const preview = byId<HTMLPreElement>('preview')

/** Random bytes as lowercase hex, the shape OTLP/JSON wants for trace and span ids. */
function randomHex(bytes: number): string {
  const buffer = new Uint8Array(bytes)
  crypto.getRandomValues(buffer)
  return [...buffer].map((b) => b.toString(16).padStart(2, '0')).join('')
}

function buildPayload(message: string, severityNumber: number) {
  // Unix nanoseconds, as a string: JSON numbers cannot hold them exactly.
  const nowNanos = (BigInt(Date.now()) * 1000000n).toString()

  return {
    resourceLogs: [
      {
        resource: {
          attributes: [
            { key: 'service.name', value: { stringValue: 'browser-sample' } },
            { key: 'service.instance.id', value: { stringValue: location.host } },
          ],
        },
        scopeLogs: [
          {
            scope: { name: 'browser.sample' },
            logRecords: [
              {
                timeUnixNano: nowNanos,
                observedTimeUnixNano: nowNanos,
                severityNumber,
                severityText: severitySelect.selectedOptions[0]?.text ?? '',
                body: { stringValue: message },
                traceId: randomHex(16),
                spanId: randomHex(8),
                attributes: [
                  { key: 'page.url', value: { stringValue: location.href } },
                  { key: 'user_agent.original', value: { stringValue: navigator.userAgent } },
                ],
              },
            ],
          },
        ],
      },
    ],
  }
}

function refreshPreview() {
  preview.textContent = JSON.stringify(
    buildPayload(messageInput.value, Number(severitySelect.value)),
    null,
    2,
  )
}

messageInput.addEventListener('input', refreshPreview)
severitySelect.addEventListener('change', refreshPreview)
refreshPreview()

sendButton.addEventListener('click', async () => {
  const key = keyInput.value.trim()
  if (key.length === 0) {
    result.textContent = 'Enter a producer API key first.'
    result.className = 'error'
    return
  }

  sendButton.disabled = true
  result.textContent = 'Sending...'
  result.className = ''

  try {
    const response = await fetch('/v1/logs', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', 'X-Api-Key': key },
      body: JSON.stringify(buildPayload(messageInput.value, Number(severitySelect.value))),
    })

    if (response.ok) {
      result.textContent = 'Accepted (' + response.status + '). Check the live view.'
      result.className = 'ok'
    } else if (response.status === 401) {
      result.textContent = 'The server rejected that key (401).'
      result.className = 'error'
    } else if (response.status === 429) {
      result.textContent = 'Rate limited (429). Wait a moment.'
      result.className = 'error'
    } else {
      result.textContent = 'The server replied ' + response.status + '.'
      result.className = 'error'
    }
  } catch {
    result.textContent = 'The request could not be sent.'
    result.className = 'error'
  } finally {
    sendButton.disabled = false
  }
})
