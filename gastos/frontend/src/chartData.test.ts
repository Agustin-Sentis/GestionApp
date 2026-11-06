import { describe, expect, it } from 'vitest'
import type { CategoryTotal, MonthlyReport } from './api.ts'
import { buildChartView, categoryOrder, colorOf, defaultChartKind, isChartKind, MAX_DONUT_SLICES, percentOf } from './chartData.ts'
import type { ChartView } from './chartData.ts'

const expense = (name: string, total: number): CategoryTotal => ({ name, type: 'Expense', total })
const income = (name: string, total: number): CategoryTotal => ({ name, type: 'Income', total })

function report(rows: CategoryTotal[]): MonthlyReport {
  const totalOf = (type: string) => rows.filter((r) => r.type === type).reduce((acc, r) => acc + r.total, 0)
  return {
    year: 2026, month: 11, currency: 'ARS',
    income: totalOf('Income'), expense: totalOf('Expense'), balance: totalOf('Income') - totalOf('Expense'),
    byCategory: rows, pendingConversions: [], hasUnconvertedUsd: false, previous: null,
  }
}

const ORDER = ['alquiler', 'comida', 'transporte', 'sueldo']

const BASE = report([income('Sueldo', 100000), expense('Alquiler', 40000), expense('Comida', 25000), expense('Transporte', 5000)])

/** The parts of a chart that is not empty. */
function drawn(view: ChartView) {
  if (view.shape === 'empty') throw new Error(`Expected a chart but got: ${view.message}`)
  return view
}

describe('percentOf', () => {
  it('is the share of the base, as 0-100', () => {
    expect(percentOf(25, 200)).toBe(12.5)
    expect(percentOf(200, 200)).toBe(100)
  })

  it('is 0 when there is no base, instead of Infinity or NaN', () => {
    expect(percentOf(50, 0)).toBe(0)
    expect(percentOf(0, 0)).toBe(0)
    expect(percentOf(50, -10)).toBe(0)
  })
})

describe('isChartKind', () => {
  it('accepts the charts that exist and nothing else', () => {
    expect(isChartKind('destino')).toBe(true)
    expect(isChartKind('comparacion')).toBe(true)
    expect(isChartKind('torta')).toBe(false)
    expect(isChartKind('')).toBe(false)
    expect(isChartKind(null)).toBe(false)
  })
})

describe('colorOf', () => {
  it('depends on the place of the category among all of the user\'s categories', () => {
    expect(colorOf('Alquiler', ORDER, 0)).toBe('var(--series-1)')
    expect(colorOf('Transporte', ORDER, 0)).toBe('var(--series-3)')
  })

  it('ignores case and spaces around the name', () => {
    expect(colorOf('  COMIDA ', ORDER, 0)).toBe('var(--series-2)')
  })

  it('never invents a ninth hue: the categories past the eighth share one neutral color', () => {
    const many = Array.from({ length: 12 }, (_, i) => `categoria ${i}`)

    expect(colorOf('categoria 7', many, 0)).toBe('var(--series-8)')
    expect(colorOf('categoria 8', many, 0)).toBe('var(--chart-other)')
    expect(colorOf('categoria 11', many, 0)).toBe('var(--chart-other)')
  })

  it('uses the place in the chart only when the categories could not be loaded', () => {
    expect(colorOf('Cualquiera', [], 2)).toBe('var(--series-3)')
    expect(colorOf('Cualquiera', [], 9)).toBe('var(--chart-other)')
  })

  it('is neutral for a category that is not in the list, when the list is there', () => {
    expect(colorOf('Borrada', ORDER, 0)).toBe('var(--chart-other)')
  })
})

describe('categoryOrder', () => {
  it('lists the names in the order given, in lower case', () => {
    expect(categoryOrder([
      { id: '1', name: 'Comida', type: 'Expense' },
      { id: '2', name: ' Sueldo ', type: 'Income' },
    ])).toEqual(['comida', 'sueldo'])
  })
})

describe('defaultChartKind', () => {
  it('opens with where the income went, when there is income', () => {
    expect(defaultChartKind(BASE)).toBe('destino')
  })

  it('opens with the expenses by category when there is no income', () => {
    expect(defaultChartKind(report([expense('Comida', 100)]))).toBe('gastos')
  })
})

describe('chart "gastos"', () => {
  it('splits the expenses, biggest first, as shares of the expenses', () => {
    const view = drawn(buildChartView('gastos', BASE, ORDER))

    expect(view.shape).toBe('donut')
    expect(view.slices.map((s) => s.name)).toEqual(['Alquiler', 'Comida', 'Transporte'])
    expect(view.slices.map((s) => Math.round(s.percent))).toEqual([57, 36, 7])
    expect(view.slices.reduce((acc, s) => acc + s.percent, 0)).toBeCloseTo(100)
    expect(view.percentLabel).toBe('% de los gastos')
  })

  it('does not count the income at all: it is the "expenses" chart', () => {
    const withMoreIncome = report([income('Sueldo', 900000), expense('Alquiler', 40000), expense('Comida', 25000)])

    const view = drawn(buildChartView('gastos', withMoreIncome, ORDER))

    expect(view.slices.map((s) => Math.round(s.percent))).toEqual([62, 38])
  })

  it('says so when there are no expenses but there is income', () => {
    expect(buildChartView('gastos', report([income('Sueldo', 5)]), ORDER)).toEqual({
      shape: 'empty', message: 'No hay gastos cargados en este mes.',
    })
  })

  it('says there is nothing yet when the month is empty', () => {
    expect(buildChartView('gastos', report([]), ORDER)).toEqual({
      shape: 'empty', message: 'Todavía no hay movimientos cargados en este mes.',
    })
  })

  it('leaves out categories that add up to nothing', () => {
    const view = drawn(buildChartView('gastos', report([expense('Comida', 100), expense('Vacía', 0)]), ORDER))

    expect(view.slices.map((s) => s.name)).toEqual(['Comida'])
  })
})

describe('chart "ingresos"', () => {
  it('splits the income by category', () => {
    const view = drawn(buildChartView('ingresos', report([income('Sueldo', 75), income('Extras', 25)]), ORDER))

    expect(view.slices.map((s) => s.percent)).toEqual([75, 25])
    expect(view.percentLabel).toBe('% de los ingresos')
  })

  it('says so when there is no income', () => {
    expect(buildChartView('ingresos', report([expense('Comida', 5)]), ORDER)).toEqual({
      shape: 'empty', message: 'No hay ingresos cargados en este mes.',
    })
  })
})

describe('chart "destino" (where the income went)', () => {
  it('shows every expense as a share of the INCOME, and what was not spent', () => {
    const view = drawn(buildChartView('destino', BASE, ORDER))

    expect(view.slices.map((s) => [s.name, s.percent])).toEqual([
      ['Alquiler', 40],
      ['Comida', 25],
      ['Transporte', 5],
      ['Sin gastar', 30],
    ])
    expect(view.slices.reduce((acc, s) => acc + s.percent, 0)).toBeCloseTo(100)
    expect(view.slices.at(-1)).toMatchObject({ value: 30000, color: 'var(--chart-rest)' })
    expect(view.note).toBe('Gastaste el 70% de lo que ingresó este mes.')
  })

  it('changes every percentage when another income is added, which is the point of this chart', () => {
    const before = drawn(buildChartView('destino', BASE, ORDER))
    const afterAnIncome = report([income('Sueldo', 100000), income('Extras', 50000), expense('Alquiler', 40000), expense('Comida', 25000), expense('Transporte', 5000)])

    const after = drawn(buildChartView('destino', afterAnIncome, ORDER))

    expect(before.slices.find((s) => s.name === 'Alquiler')?.percent).toBe(40)
    expect(after.slices.find((s) => s.name === 'Alquiler')?.percent).toBeCloseTo(26.67, 1)
    expect(after.slices.find((s) => s.name === 'Sin gastar')?.percent).toBeCloseTo(53.33, 1)
    expect(after.slices.reduce((acc, s) => acc + s.percent, 0)).toBeCloseTo(100)
  })

  it('has no "Sin gastar" when everything that came in was spent', () => {
    const view = drawn(buildChartView('destino', report([income('Sueldo', 100), expense('Comida', 100)]), ORDER))

    expect(view.slices.map((s) => s.name)).toEqual(['Comida'])
    expect(view.note).toBe('Gastaste el 100% de lo que ingresó este mes.')
  })

  it('explains that the expenses are above the income, and gives the shares over the income', () => {
    const view = drawn(buildChartView('destino', report([income('Sueldo', 100), expense('Alquiler', 120), expense('Comida', 30)]), ORDER))

    expect(view.slices.map((s) => s.name)).toEqual(['Alquiler', 'Comida'])
    expect(view.slices.map((s) => s.percent)).toEqual([120, 30])
    expect(view.note).toContain('Los gastos superan los ingresos')
    expect(view.note).toContain('150%')
  })

  it('asks for an income when there is none, instead of dividing by zero', () => {
    const view = buildChartView('destino', report([expense('Comida', 100)]), ORDER)

    expect(view).toMatchObject({ shape: 'empty' })
    expect(view.shape === 'empty' && view.message).toContain('cargá uno')
  })

  it('shows all of the income as unspent when there are no expenses', () => {
    const view = drawn(buildChartView('destino', report([income('Sueldo', 100)]), ORDER))

    expect(view.slices).toHaveLength(1)
    expect(view.slices[0]).toMatchObject({ name: 'Sin gastar', percent: 100 })
  })
})

describe('donuts with many categories', () => {
  const many = (n: number) => Array.from({ length: n }, (_, i) => expense(`Cat ${i + 1}`, (n - i) * 100))

  it('keeps the biggest and folds the rest into "Otras", so a donut never has more than six parts', () => {
    const view = drawn(buildChartView('gastos', report(many(10)), []))

    expect(view.slices).toHaveLength(MAX_DONUT_SLICES)
    expect(view.slices.at(-1)).toMatchObject({ name: 'Otras', color: 'var(--chart-other)' })
    expect(view.slices.slice(0, -1).map((s) => s.name)).toEqual(['Cat 1', 'Cat 2', 'Cat 3', 'Cat 4', 'Cat 5'])
    // 10+9+...+1 = 55; the folded ones are 5+4+3+2+1 = 15 of 55
    expect(view.slices.at(-1)?.value).toBe(1500)
    expect(view.slices.reduce((acc, s) => acc + s.percent, 0)).toBeCloseTo(100)
  })

  it('does not fold anything when exactly six fit', () => {
    const view = drawn(buildChartView('gastos', report(many(6)), []))

    expect(view.slices).toHaveLength(6)
    expect(view.slices.some((s) => s.name === 'Otras')).toBe(false)
  })

  it('leaves room for "Sin gastar" in the chart of the income', () => {
    const view = drawn(buildChartView('destino', report([income('Sueldo', 100000), ...many(10)]), []))

    expect(view.slices).toHaveLength(MAX_DONUT_SLICES)
    expect(view.slices.at(-1)?.name).toBe('Sin gastar')
    expect(view.slices.at(-2)?.name).toBe('Otras')
  })
})

describe('colors stay with the category', () => {
  it('gives a category the same color whatever else is on the chart', () => {
    const all = drawn(buildChartView('gastos', BASE, ORDER))
    const withoutFood = drawn(buildChartView('gastos', report([expense('Alquiler', 40000), expense('Transporte', 5000)]), ORDER))

    const colorIn = (view: { slices: { name: string; color: string }[] }, name: string) =>
      view.slices.find((s) => s.name === name)?.color

    expect(colorIn(withoutFood, 'Transporte')).toBe(colorIn(all, 'Transporte'))
    expect(colorIn(withoutFood, 'Alquiler')).toBe(colorIn(all, 'Alquiler'))
  })

  it('uses the same color for a category in the donut and in the ranking', () => {
    const donut = drawn(buildChartView('gastos', BASE, ORDER))
    const ranking = drawn(buildChartView('ranking', BASE, ORDER))

    expect(ranking.slices.find((s) => s.name === 'Comida')?.color).toBe(donut.slices.find((s) => s.name === 'Comida')?.color)
  })
})

describe('chart "ranking"', () => {
  it('lists every category of both types, biggest first, each with its type', () => {
    const view = drawn(buildChartView('ranking', BASE, ORDER))

    expect(view.shape).toBe('bars')
    expect(view.slices.map((s) => [s.name, s.detail])).toEqual([
      ['Sueldo', 'Ingreso'],
      ['Alquiler', 'Gasto'],
      ['Comida', 'Gasto'],
      ['Transporte', 'Gasto'],
    ])
  })

  it('measures each category against the total of its own type', () => {
    const view = drawn(buildChartView('ranking', BASE, ORDER))

    expect(view.slices.find((s) => s.name === 'Sueldo')?.percent).toBe(100)
    expect(view.slices.find((s) => s.name === 'Alquiler')?.percent).toBeCloseTo(57.14, 1)
    expect(view.percentLabel).toBe('% de su tipo')
  })

  it('shows only the ten biggest, and says so', () => {
    const rows = Array.from({ length: 13 }, (_, i) => expense(`Cat ${i + 1}`, (20 - i) * 10))

    const view = drawn(buildChartView('ranking', report(rows), []))

    expect(view.slices).toHaveLength(10)
    expect(view.note).toBe('Se muestran las 10 categorías con más movimiento.')
  })

  it('is empty when the month has nothing', () => {
    expect(buildChartView('ranking', report([]), ORDER)).toMatchObject({ shape: 'empty' })
  })
})

describe('chart "comparacion"', () => {
  it('puts the income and the expenses side by side, the expenses as a share of the income', () => {
    const view = drawn(buildChartView('comparacion', BASE, ORDER))

    expect(view.shape).toBe('bars')
    expect(view.slices.map((s) => [s.name, s.value, s.percent])).toEqual([
      ['Ingresos', 100000, 100],
      ['Gastos', 70000, 70],
    ])
    expect(view.note).toBe('Gastaste el 70% de lo que ingresó este mes.')
  })

  it('still shows the expenses when there is no income, without a percentage that means nothing', () => {
    const view = drawn(buildChartView('comparacion', report([expense('Comida', 100)]), ORDER))

    expect(view.slices.find((s) => s.name === 'Gastos')?.percent).toBe(0)
    expect(view.note).toBeNull()
  })

  it('is empty when there is neither income nor expenses', () => {
    expect(buildChartView('comparacion', report([]), ORDER)).toMatchObject({ shape: 'empty' })
  })
})
