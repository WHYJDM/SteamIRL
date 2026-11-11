// Web Mercator coordinates in pixels at an integer zoom. Longitude is bounded
// rather than wrapped: this local discovery map does not cross the antimeridian.
export const MAX_LAT = 85.05112878
export function project(latitude: number, longitude: number, zoom: number): [number, number] {
  const size = 256 * 2 ** zoom
  const lat = Math.max(-MAX_LAT, Math.min(MAX_LAT, latitude)) * Math.PI / 180
  return [(longitude + 180) / 360 * size, (1 - Math.asinh(Math.tan(lat)) / Math.PI) / 2 * size]
}
export function unproject(x: number, y: number, zoom: number): [number, number] {
  const size = 256 * 2 ** zoom
  return [Math.atan(Math.sinh(Math.PI * (1 - 2 * y / size))) * 180 / Math.PI, x / size * 360 - 180]
}
export function clampCenter(x: number, y: number, zoom: number, width: number, height: number): [number, number] {
  const size = 256 * 2 ** zoom
  return [Math.max(width / 2, Math.min(size - width / 2, x)), Math.max(height / 2, Math.min(size - height / 2, y))]
}
