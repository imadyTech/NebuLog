import { useCallback, useEffect, useState, type FormEvent } from 'react'
import { api } from '../../api/client'
import type { ApiKeyDto, CreatedApiKeyDto } from '../../api/contracts'
import styles from './KeysPage.module.css'

/** Issue and revoke producer API keys. Administrators only. */
export default function KeysPage() {
  const [keys, setKeys] = useState<ApiKeyDto[]>([])
  const [name, setName] = useState('')
  const [serviceName, setServiceName] = useState('')
  const [created, setCreated] = useState<CreatedApiKeyDto | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  const refresh = useCallback(async () => {
    try {
      setKeys(await api.listKeys())
    } catch {
      setError('Could not load the key list.')
    }
  }, [])

  useEffect(() => {
    void refresh()
  }, [refresh])

  async function create(event: FormEvent) {
    event.preventDefault()
    setBusy(true)
    setError(null)

    try {
      setCreated(await api.createKey(name.trim(), serviceName.trim() || undefined))
      setName('')
      setServiceName('')
      await refresh()
    } catch {
      setError('Could not create the key. A name is required.')
    } finally {
      setBusy(false)
    }
  }

  async function revoke(key: ApiKeyDto) {
    setBusy(true)
    setError(null)

    try {
      await api.revokeKey(key.id)
      await refresh()
    } catch {
      setError('Could not revoke the key.')
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className={styles.page}>
      <h1 className={styles.heading}>API keys</h1>
      <p className={styles.intro}>
        Producers authenticate with a key in the <code>X-Api-Key</code> header. Only a hash is stored, so a key is
        shown once and cannot be recovered afterwards.
      </p>

      <form className={styles.form} onSubmit={create}>
        <input
          className={styles.input}
          placeholder="Name, e.g. orders-api"
          value={name}
          onChange={(event) => setName(event.target.value)}
          required
          aria-label="Key name"
        />
        <input
          className={styles.input}
          placeholder="Service name (optional)"
          value={serviceName}
          onChange={(event) => setServiceName(event.target.value)}
          aria-label="Service name"
        />
        <button type="submit" className={styles.primary} disabled={busy}>
          Create key
        </button>
      </form>

      {error !== null && (
        <p className={styles.error} role="alert">
          {error}
        </p>
      )}

      {created !== null && <NewKeyNotice created={created} onDismiss={() => setCreated(null)} />}

      <table className={styles.table}>
        <thead>
          <tr>
            <th>Name</th>
            <th>Prefix</th>
            <th>Service</th>
            <th>Created</th>
            <th>Last used</th>
            <th>Status</th>
            <th aria-label="Actions" />
          </tr>
        </thead>
        <tbody>
          {keys.length === 0 ? (
            <tr>
              <td colSpan={7} className={styles.empty}>
                No keys issued yet.
              </td>
            </tr>
          ) : (
            keys.map((key) => (
              <tr key={key.id}>
                <td>{key.name}</td>
                <td className={styles.mono}>{key.prefix}</td>
                <td>{key.serviceName ?? '—'}</td>
                <td>{formatDate(key.createdAt)}</td>
                <td>{key.lastUsedAt === null ? 'never' : formatDate(key.lastUsedAt)}</td>
                <td className={key.revokedAt === null ? styles.active : styles.revoked}>
                  {key.revokedAt === null ? 'active' : 'revoked'}
                </td>
                <td>
                  {key.revokedAt === null && (
                    <button type="button" className={styles.danger} disabled={busy} onClick={() => void revoke(key)}>
                      Revoke
                    </button>
                  )}
                </td>
              </tr>
            ))
          )}
        </tbody>
      </table>
    </div>
  )
}

function NewKeyNotice({ created, onDismiss }: { created: CreatedApiKeyDto; onDismiss: () => void }) {
  const [copied, setCopied] = useState(false)

  async function copy() {
    try {
      await navigator.clipboard.writeText(created.apiKey)
      setCopied(true)
    } catch {
      // Clipboard access can be refused; the value stays selectable.
    }
  }

  return (
    <div className={styles.notice} role="alert">
      <strong className={styles.noticeTitle}>Copy this key now — it will not be shown again.</strong>
      <div className={styles.keyRow}>
        <code className={styles.key}>{created.apiKey}</code>
        <button type="button" className={styles.primary} onClick={() => void copy()}>
          {copied ? 'Copied' : 'Copy'}
        </button>
        <button type="button" className={styles.ghost} onClick={onDismiss}>
          Dismiss
        </button>
      </div>
    </div>
  )
}

function formatDate(value: string): string {
  const parsed = new Date(value)
  return Number.isNaN(parsed.getTime()) ? value : parsed.toLocaleString()
}
