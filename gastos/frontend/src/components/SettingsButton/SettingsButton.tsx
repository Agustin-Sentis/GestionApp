interface Props {
  onClick: () => void
}

/**
 * The button of the header that opens the settings window, beside "Salir". On a phone only the gear is drawn (the header
 * is short of room); from sm up it says "Ajustes". It used to float at the bottom left, and covered the end of the panels.
 */
const SettingsButton = ({ onClick }: Props) => {
  return (
    <button type="button" onClick={onClick} className="btn inline-flex items-center justify-center gap-2 max-sm:px-2.5 pointer-coarse:min-h-10 max-sm:pointer-coarse:min-w-10">
      <span aria-hidden="true">⚙</span>
      {/* On a phone only the gear is drawn; the word stays for whoever listens to the page */}
      <span className="max-sm:sr-only">Ajustes</span>
    </button>
  )
}

export default SettingsButton
