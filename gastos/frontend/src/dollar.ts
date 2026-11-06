import type { DollarType } from './api.ts'

/** The name of each dollar to write in the middle of a sentence: "cotización blue", "la cotización oficial". */
export const DOLLAR_NAMES: Record<DollarType, string> = {
  Official: 'oficial',
  Blue: 'blue',
  Mep: 'MEP',
}

/** The name of each dollar as a label of its own: "Oficial", "Blue", "MEP". */
export const DOLLAR_LABELS: Record<DollarType, string> = {
  Official: 'Oficial',
  Blue: 'Blue',
  Mep: 'MEP',
}

/** What each dollar is, for the note that appears when its button is hovered or focused. */
export const DOLLAR_HINTS: Record<DollarType, string> = {
  Official: 'El dólar oficial.',
  Blue: 'El dólar blue, el del mercado informal.',
  Mep: 'El dólar MEP, también llamado bolsa.',
}

/**
 * What choosing a dollar does, said in every note: it applies from now on, and what is already saved is left alone.
 * It is the one thing people get wrong about this control, so it is repeated where they look.
 */
export const DOLLAR_SCOPE = 'Se usa para los movimientos que cargues de ahora en adelante: los que ya cargaste conservan el suyo.'
