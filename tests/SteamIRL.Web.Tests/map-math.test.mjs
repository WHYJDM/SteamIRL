import { test } from 'node:test'
import assert from 'node:assert/strict'
import { project, unproject, clampCenter, MAX_LAT } from '../../src/SteamIRL.Web/src/locations/mapMath.ts'

test('Vilnius coordinates survive projection at every supported zoom', () => {
  for (let zoom = 10; zoom <= 18; zoom++) {
    for (const [lat, lon] of [[54.6948, 25.2764], [54.6764, 25.3161], [-54, -25]]) {
      const [actualLat, actualLon] = unproject(...project(lat, lon, zoom), zoom)
      assert(Math.abs(actualLat - lat) < 1e-8)
      assert(Math.abs(actualLon - lon) < 1e-8)
    }
  }
})
test('poles clamp to finite Web Mercator pixels', () => {
  assert(project(90, 25, 13).every(Number.isFinite))
  assert.deepEqual(project(90, 25, 13), project(MAX_LAT, 25, 13))
  assert.deepEqual(project(-90, 25, 13), project(-MAX_LAT, 25, 13))
})
test('map centre stays within the world for all allowed zooms', () => {
  for (let zoom = 10; zoom <= 18; zoom++) {
    const size = 256 * 2 ** zoom
    assert.deepEqual(clampCenter(-100, -100, zoom, 600, 420), [300, 210])
    assert.deepEqual(clampCenter(size + 100, size + 100, zoom, 600, 420), [size - 300, size - 210])
  }
})
test('initial Vilnius viewport covers both seeded complexes', () => {
  const [cx, cy] = project(54.6856, 25.295, 13)
  for (const [lat, lon] of [[54.6948, 25.2764], [54.6764, 25.3161]]) {
    const [x, y] = project(lat, lon, 13)
    assert(Math.abs(x - cx) < 600 / 2)
    assert(Math.abs(y - cy) < 420 / 2)
  }
})
