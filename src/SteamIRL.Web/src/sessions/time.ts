const zone = 'Europe/Vilnius'
const partsFormatter = new Intl.DateTimeFormat('en-GB', { timeZone: zone, year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit', hourCycle: 'h23' })
function parts(timestamp: number) {
  const values = Object.fromEntries(partsFormatter.formatToParts(timestamp).map(part => [part.type, part.value]))
  return `${values.year}-${values.month}-${values.day}T${values.hour}:${values.minute}`
}
export const vilniusInput = (timestamp: number) => parts(timestamp)
export function vilniusToIso(input: string): string {
  if (!/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}$/.test(input)) throw new Error('Укажи дату и время по Вильнюсу.')
  const naive = Date.parse(input + ':00Z')
  if (!Number.isFinite(naive)) throw new Error('Некорректная дата.')
  const offsets = new Set([-86400000, 0, 86400000].map(delta => {
    const probe = naive + delta
    return Date.parse(parts(probe) + ':00Z') - probe
  }))
  const matches = [...offsets].map(offset => naive - offset).filter(candidate => parts(candidate) === input)
  if (matches.length === 0) throw new Error('Такого времени нет при переходе на летнее время. Выбери другое время.')
  if (matches.length > 1) throw new Error('Это время повторяется при переводе часов. Выбери однозначное время.')
  return new Date(matches[0]).toISOString()
}
const display = new Intl.DateTimeFormat('ru', { timeZone: zone, day: 'numeric', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit', timeZoneName: 'short' })
export const sessionTime = (value: string) => display.format(new Date(value))
