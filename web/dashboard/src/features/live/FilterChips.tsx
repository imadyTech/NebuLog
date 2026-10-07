import styles from './FilterChips.module.css'

/** One removable filter. */
export interface Chip {
  key: string
  label: string
  onRemove: () => void
}

interface Props {
  chips: readonly Chip[]
  shown: number
  buffered: number
}

/**
 * Shows every active filter as a chip that can be removed on its own.
 *
 * The count beside them answers the question the filters raise: "N of M in buffer" makes it
 * obvious when an empty table means a narrow filter rather than a silent server.
 */
export default function FilterChips({ chips, shown, buffered }: Props) {
  if (chips.length === 0) {
    return null
  }

  return (
    <div className={styles.row}>
      <ul className={styles.chips}>
        {chips.map((chip) => (
          <li key={chip.key} className={styles.chip}>
            <span>{chip.label}</span>
            <button
              type="button"
              className={styles.remove}
              onClick={chip.onRemove}
              aria-label={`Remove filter ${chip.label}`}
            >
              ×
            </button>
          </li>
        ))}
      </ul>
      <span className={styles.count}>
        {shown.toLocaleString()} of {buffered.toLocaleString()} in buffer
      </span>
    </div>
  )
}
