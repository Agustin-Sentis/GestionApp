import { useState } from 'react'
import { downloadBlob } from '../download.ts'
import { useToast } from './toastContext.ts'

interface Props {
  codes: string[]
  /** The user said they saved the codes and pressed "Listo": they are not shown again. */
  onDone: () => void
}

/** The text of the downloaded file: a short explanation, then one code per line. */
function fileText(codes: string[]): string {
  return [
    'Códigos de recuperación de DualBalance',
    'Guardalos en un lugar seguro. Cada uno sirve una sola vez, en lugar del código de tu app autenticadora.',
    '',
    ...codes,
    '',
  ].join('\n')
}

/**
 * The recovery codes, shown once: the server keeps only their hashes, so after this nobody can see them again (not even
 * the user). "Listo" stays disabled until the user says they saved them.
 */
export default function RecoveryCodes({ codes, onDone }: Props) {
  const toast = useToast()
  const [saved, setSaved] = useState(false)

  async function copy() {
    try {
      await navigator.clipboard.writeText(codes.join('\n'))
      toast.success('Códigos copiados')
    } catch {
      toast.error('No se pudieron copiar. Descargalos o anotalos a mano.')
    }
  }

  return (
    <div className="flex flex-col gap-3">
      <p className="text-sm font-bold">Paso 3 de 3 · Guardá tus códigos de recuperación</p>
      <p className="text-sm">
        Si perdés el celular, cada uno de estos códigos te deja entrar <strong>una sola vez</strong> en lugar del código de la
        app. <strong>No los vas a poder ver de nuevo.</strong> Guardalos en un lugar seguro, fuera de este dispositivo.
      </p>

      <ul className="grid grid-cols-2 gap-x-4 gap-y-1 rounded-sm border-2 border-ink bg-paper p-3 font-mono text-sm">
        {codes.map((code) => (
          <li key={code}>{code}</li>
        ))}
      </ul>

      <div className="flex flex-wrap items-center gap-2">
        <button type="button" className="btn" onClick={copy}>Copiar códigos</button>
        <button
          type="button"
          className="btn"
          onClick={() => downloadBlob(new Blob([fileText(codes)], { type: 'text/plain;charset=utf-8' }), 'codigos-de-recuperacion-dualbalance.txt')}
        >
          Descargar .txt
        </button>
      </div>

      <label className="flex cursor-pointer items-start gap-2.5 text-sm font-semibold">
        <input type="checkbox" className="mt-0.5 size-4 shrink-0" checked={saved} onChange={(e) => setSaved(e.target.checked)} />
        <span>Ya guardé los códigos en un lugar seguro</span>
      </label>

      <button type="button" className="btn-primary self-start" disabled={!saved} onClick={onDone}>
        Listo
      </button>
    </div>
  )
}
