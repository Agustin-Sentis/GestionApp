interface Props {
  children: string
  /** What the button says ("Agregar un gasto"). Without it, or without `onAction`, the note has no button. */
  actionLabel?: string
  onAction?: () => void
}

/**
 * What a part of the screen says when it has nothing to show yet. It is written by hand, like a note in the margin of the
 * notebook (the one place where the page "speaks" to the person), and it points to what to do next instead of leaving
 * an empty chart or a blank table.
 */
const EmptyNote = ({ children, actionLabel, onAction }: Props) => {
  return (
    <div className="flex flex-col items-start gap-1 py-2">
      <p className="hand text-lg leading-snug">{children}</p>
      {actionLabel && onAction && (
        <button type="button" className="btn-link" onClick={onAction}>
          {actionLabel}
        </button>
      )}
    </div>
  )
}

export default EmptyNote
