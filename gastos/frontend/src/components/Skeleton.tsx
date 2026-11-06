import type { ReactNode } from 'react'

/** A grey placeholder block with the size of the content that is about to appear. Pass the size in className. */
export function Skeleton({ className = '' }: { className?: string }) {
  return (
    <div
      // Decorative: the group around it says what is loading
      aria-hidden="true"
      // motion-safe: the pulse is skipped for people who asked their system for less movement
      className={`rounded-md bg-slate-200 motion-safe:animate-pulse dark:bg-slate-800 ${className}`}
    />
  )
}

/**
 * Wraps the placeholders of one loading section. Screen readers hear the label once
 * ("Cargando el resumen…") instead of a list of empty blocks.
 */
export function SkeletonGroup({ label, className = '', children }: { label: string; className?: string; children: ReactNode }) {
  return (
    <div role="status" aria-busy="true" className={className}>
      <span className="sr-only">{label}</span>
      {children}
    </div>
  )
}
