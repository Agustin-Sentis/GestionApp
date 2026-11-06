import { Link } from 'react-router-dom'
import Brand, { APP_NAME } from '../components/Brand.tsx'
import ThemeToggle from '../components/ThemeToggle.tsx'
import { formatDate, formatMoney } from '../format.ts'
import { CAPABILITIES, LATER, SAMPLE_LINES, sampleTotals } from '../landingContent.ts'

export default function LandingPage() {
  const totals = sampleTotals()

  return (
    <div className="relative min-h-dvh">
      {/* The margin of the notebook page, running down the whole page */}
      <div
        aria-hidden="true"
        className="pointer-events-none absolute inset-y-0 left-3 w-[5px] border-r-4 border-double border-margin sm:left-14"
      />

      <div className="mx-auto max-w-6xl pr-4 pl-8 sm:pr-8 sm:pl-24">
        <header className="flex flex-wrap items-center justify-between gap-4 py-5">
          <Brand />
          <nav aria-label="Acceso" className="flex flex-wrap items-center gap-3">
            <ThemeToggle />
            <Link to="/login" className="btn-link">Entrar</Link>
            <Link to="/login?modo=registro" className="btn-primary inline-block text-center">Crear cuenta</Link>
          </nav>
        </header>

        <main>
          <section className="grid gap-12 py-10 lg:grid-cols-[1.05fr_0.95fr] lg:items-center lg:py-20">
            <div className="flex flex-col items-start gap-6">
              <h1 className="display text-[clamp(2.2rem,5.4vw,4.1rem)] leading-[1.02] break-words">
                Anotá lo que gastás.
                <br />
                Sabé <span className="marker paint-in">lo que te queda.</span>
              </h1>
              <p className="max-w-prose text-lg leading-relaxed">
                {APP_NAME} es un cuaderno de gastos para quien maneja pesos y dólares. Anotá cada movimiento en la
                moneda que usaste: el mes se suma en pesos, con la cotización del día en que lo cargaste.
              </p>
              <div className="flex flex-wrap items-center gap-4">
                <Link to="/login?modo=registro" className="btn-primary inline-block px-5 py-2.5 text-center text-base">
                  Crear cuenta
                </Link>
                <Link to="/login" className="btn-link text-base">Ya tengo cuenta</Link>
              </div>
            </div>

            <figure aria-label="Ejemplo de una hoja del libro" className="card gap-0 p-0 lg:rotate-1">
              <figcaption className="panel-title mx-5 mt-5">Noviembre · hoja de ejemplo</figcaption>
              <table className="mx-2 mt-2 text-sm">
                <tbody>
                  {SAMPLE_LINES.map((line, index) => (
                    <tr
                      key={line.date}
                      className="write-in border-b border-line"
                      style={{ '--delay': `${0.25 + index * 0.18}s` } as React.CSSProperties}
                    >
                      <td className="figure p-2 text-xs whitespace-nowrap text-ink-soft">{formatDate(line.date).slice(0, 5)}</td>
                      <td className="p-2">
                        {line.concept}
                        {line.aside && <div className="hand text-xs font-semibold">{line.aside}</div>}
                      </td>
                      <td
                        className={`figure p-2 text-right text-xs whitespace-nowrap ${line.amount < 0 ? 'text-expense' : 'text-income'}`}
                      >
                        {line.amount < 0 ? '−' : '+'}
                        {formatMoney(Math.abs(line.amount))}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
              <dl className="grid grid-cols-3 gap-3 p-5">
                <div>
                  <dt className="muted text-[0.7rem] font-semibold tracking-wide uppercase">Ingresos</dt>
                  <dd className="figure text-xs text-income sm:text-sm">{formatMoney(totals.income)}</dd>
                </div>
                <div>
                  <dt className="muted text-[0.7rem] font-semibold tracking-wide uppercase">Gastos</dt>
                  <dd className="figure text-xs text-expense sm:text-sm">{formatMoney(totals.expense)}</dd>
                </div>
                <div>
                  <dt className="muted text-[0.7rem] font-semibold tracking-wide uppercase">Te queda</dt>
                  <dd>
                    <span
                      className="marker paint-in figure border-b-[3px] border-double border-ink text-xs font-bold sm:text-sm"
                      style={{ '--delay': '1.1s' } as React.CSSProperties}
                    >
                      {formatMoney(totals.left)}
                    </span>
                  </dd>
                </div>
              </dl>
            </figure>
          </section>

          <section aria-labelledby="que-podes-hacer" className="py-12">
            <h2 id="que-podes-hacer" className="panel-title max-w-md">Qué podés hacer</h2>
            <dl className="mt-2">
              {CAPABILITIES.map((item) => (
                <div key={item.concept} className="grid gap-1 border-b border-line py-5 sm:grid-cols-[13rem_1fr] sm:gap-8">
                  <dt className="display text-base uppercase" style={{ letterSpacing: '0.03em' }}>{item.concept}</dt>
                  <dd className="max-w-prose leading-relaxed">{item.what}</dd>
                </div>
              ))}
            </dl>
          </section>

          <section aria-labelledby="mas-adelante" className="py-10">
            <h2 id="mas-adelante" className="hand text-2xl font-bold">Anotado para más adelante</h2>
            <ul className="hand mt-3 flex flex-col gap-1 text-lg">
              {LATER.map((item) => (
                <li key={item}>– {item}</li>
              ))}
            </ul>
          </section>

          <section className="flex flex-col items-start gap-5 border-t-4 border-double border-ink py-14">
            <p className="display text-[clamp(1.6rem,3.6vw,2.6rem)] leading-tight">Empezá el mes con la primera línea.</p>
            <Link to="/login?modo=registro" className="btn-primary inline-block px-5 py-2.5 text-center text-base">
              Crear cuenta
            </Link>
          </section>
        </main>

        {/* The signature at the foot of the page */}
        <footer className="grid gap-6 border-t border-line pt-8 pb-12 sm:grid-cols-[1fr_auto] sm:items-end">
          <div className="flex flex-col items-start gap-2">
            <p className="muted text-[0.7rem] font-semibold tracking-wide uppercase">Hecho por</p>
            <p className="hand border-b-2 border-ink pr-10 text-3xl leading-tight font-bold">Agustín Sentis</p>
            <p className="muted text-sm">
              Rosario, Argentina. Un proyecto de portfolio hecho con .NET 10, React y PostgreSQL.
            </p>
          </div>

          <nav aria-label="Enlaces del proyecto" className="flex flex-wrap gap-x-5 gap-y-2">
            <a className="btn-link p-0" href="https://github.com/Agustin-Sentis" target="_blank" rel="noreferrer">
              GitHub
            </a>
            <a
              className="btn-link p-0"
              href="https://gestionapp-j0j4.onrender.com/scalar"
              target="_blank"
              rel="noreferrer"
            >
              Documentación de la API
            </a>
          </nav>
        </footer>
      </div>
    </div>
  )
}
