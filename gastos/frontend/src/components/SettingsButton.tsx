interface Props {
  onClick: () => void
}

/**
 * The floating button that opens the settings window. It sits at the bottom left: the notices (toasts) come up from the
 * bottom right. On a phone it is a round button with a gear, above the tab bar; from sm up it says "Ajustes".
 */
export default function SettingsButton({ onClick }: Props) {
  return (
    <button
      type="button"
      onClick={onClick}
      // Above the tab bar where there is one (narrow screens), and clear of the side margin on a wide one
      className="fixed bottom-4 left-4 z-30 inline-flex h-12 cursor-pointer items-center justify-center gap-2 rounded-full border-2 border-ink bg-sheet px-3.5 font-bold text-ink shadow-lg hover:bg-mark hover:text-on-mark focus-visible:outline-3 focus-visible:outline-pen max-xl:bottom-16 sm:rounded-sm sm:px-4 xl:left-20"
    >
      <span aria-hidden="true">⚙</span>
      {/* On a phone only the gear is drawn; the word stays for whoever listens to the page */}
      <span className="max-sm:sr-only">Ajustes</span>
    </button>
  )
}
