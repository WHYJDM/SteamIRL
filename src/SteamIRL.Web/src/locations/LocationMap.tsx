import { useEffect, useRef, useState } from 'react'
import type { KeyboardEvent, PointerEvent } from 'react'
import { clampCenter, project, unproject } from './mapMath'
import type { Bounds, SportLocation } from './api'
import './styles.css'

interface Props {
  locations: SportLocation[]
  selected?: SportLocation
  onSelect?: (location: SportLocation) => void
  onBounds?: (bounds: Bounds) => void
}

export function LocationMap({ locations, selected, onSelect, onBounds }: Props) {
  const container = useRef<HTMLDivElement>(null)
  const drag = useRef<{ x: number; y: number; cx: number; cy: number } | null>(null)
  const [size, setSize] = useState({ width: 600, height: 420 })
  const [view, setView] = useState({ latitude: 54.6856, longitude: 25.295, zoom: 13 })
  const [failed, setFailed] = useState(false)
  const [retry, setRetry] = useState(0)
  useEffect(() => {
    const observer = new ResizeObserver(entries => {
      const rect = entries[0].contentRect
      if (rect.width > 0 && rect.height > 0) setSize({ width: rect.width, height: rect.height })
    })
    if (container.current) observer.observe(container.current)
    return () => observer.disconnect()
  }, [])
  useEffect(() => {
    if (selected) setView(previous => ({ ...previous, latitude: selected.latitude, longitude: selected.longitude }))
  }, [selected])
  const [cx, cy] = project(view.latitude, view.longitude, view.zoom)
  const left = cx - size.width / 2; const top = cy - size.height / 2
  useEffect(() => {
    if (!onBounds) return
    const timer = window.setTimeout(() => {
      const [maxLat, minLon] = unproject(cx - size.width / 2, cy - size.height / 2, view.zoom)
      const [minLat, maxLon] = unproject(cx + size.width / 2, cy + size.height / 2, view.zoom)
      onBounds({ minLat: Math.max(-90, minLat), maxLat: Math.min(90, maxLat), minLon: Math.max(-180, minLon), maxLon: Math.min(180, maxLon) })
    }, 250)
    return () => window.clearTimeout(timer)
  }, [cx, cy, size, view.zoom, onBounds])

  function move(x: number, y: number) {
    const [px, py] = clampCenter(x, y, view.zoom, size.width, size.height)
    const [latitude, longitude] = unproject(px, py, view.zoom)
    setView(previous => ({ ...previous, latitude, longitude }))
  }
  function zoom(delta: number) { setView(previous => ({ ...previous, zoom: Math.max(10, Math.min(18, previous.zoom + delta)) })) }
  function pointerDown(event: PointerEvent<HTMLDivElement>) {
    if (event.button !== 0 || (event.target as Element).closest('a,button')) return
    drag.current = { x: event.clientX, y: event.clientY, cx, cy }
    event.currentTarget.setPointerCapture(event.pointerId)
  }
  function pointerMove(event: PointerEvent<HTMLDivElement>) {
    if (!drag.current) return
    const dx = event.clientX - drag.current.x; const dy = event.clientY - drag.current.y
    move(drag.current.cx - dx, drag.current.cy - dy)
  }
  function keyboard(event: KeyboardEvent<HTMLDivElement>) {
    if (event.target !== event.currentTarget) return
    const offsets: Record<string, [number, number]> = { ArrowLeft: [-100, 0], ArrowRight: [100, 0], ArrowUp: [0, -100], ArrowDown: [0, 100] }
    const offset = offsets[event.key]
    if (offset) { event.preventDefault(); move(cx + offset[0], cy + offset[1]) }
    if (event.key === '+' || event.key === '=') { event.preventDefault(); zoom(1) }
    if (event.key === '-') { event.preventDefault(); zoom(-1) }
  }
  const tiles = []
  for (let x = Math.floor(left / 256); x <= Math.floor((left + size.width) / 256); x++)
    for (let y = Math.floor(top / 256); y <= Math.floor((top + size.height) / 256); y++) {
      if (x >= 0 && y >= 0 && x < 2 ** view.zoom && y < 2 ** view.zoom)
        tiles.push(<img key={`${view.zoom}/${x}/${y}/${retry}`} className="map-tile" alt="" draggable={false} width={256} height={256}
          src={`https://tile.openstreetmap.org/${view.zoom}/${x}/${y}.png`} style={{ left: x * 256 - left, top: y * 256 - top }} onError={event => { event.currentTarget.style.visibility = 'hidden'; setFailed(true) }} />)
    }
  return <div className="map-block">
    <p className="map-help" id="map-help">Перетаскивай карту или используй стрелки, + и −. Все площадки также доступны в списке.</p>
    <div ref={container} className="location-map" tabIndex={0} role="region" aria-label="Карта спортивных площадок" aria-describedby="map-help"
      onPointerDown={pointerDown} onPointerMove={pointerMove} onPointerUp={() => { drag.current = null }} onPointerCancel={() => { drag.current = null }} onLostPointerCapture={() => { drag.current = null }} onKeyDown={keyboard}>
      {tiles}
      {failed && <span className="map-offline-label" aria-hidden="true">Фон карты недоступен</span>}
      {locations.map(location => {
        const [x, y] = project(location.latitude, location.longitude, view.zoom)
        if (x < left - 30 || x > left + size.width + 30 || y < top - 40 || y > top + size.height + 40) return null
        return onSelect ? <button key={location.id} type="button" className="map-marker" aria-label={`Открыть: ${location.name}`}
          aria-pressed={selected?.id === location.id} style={{ left: x - left, top: y - top }} onClick={() => onSelect(location)}><span aria-hidden="true">●</span></button>
          : <span key={location.id} className="map-marker static-marker" aria-hidden="true" style={{ left: x - left, top: y - top }}>●</span>
      })}
      <div className="map-controls"><button type="button" aria-label="Увеличить масштаб" disabled={view.zoom === 18} onClick={() => zoom(1)}>+</button><button type="button" aria-label="Уменьшить масштаб" disabled={view.zoom === 10} onClick={() => zoom(-1)}>−</button>
        <button type="button" className="map-reset" onClick={() => setView({ latitude: 54.6856, longitude: 25.295, zoom: 13 })}>Вильнюс</button></div>
      <div className="map-attribution">© <a href="https://www.openstreetmap.org/copyright" target="_blank" rel="noopener noreferrer">OpenStreetMap</a> contributors</div>
    </div>
    {failed && <div className="map-fallback" role="status"><span>Фон карты недоступен. Используй список и адрес в карточке.</span><button className="text-button" onClick={() => { setFailed(false); setRetry(value => value + 1) }}>Повторить загрузку карты</button></div>}
  </div>
}
