import test from 'node:test'
import assert from 'node:assert/strict'
import { manualPointsToGeoJson, geoJsonToManualPoints, calculatePolygonArea, calculatePolygonCentroid,
  normalizePolygonGeoJson, isPolygonInsidePolygon, isPointInsidePolygon } from '../src/utils/geo.js'

const points = [
  { latitude: 10, longitude: 106 }, { latitude: 10, longitude: 107 },
  { latitude: 11, longitude: 107 }, { latitude: 11, longitude: 106 },
]
test('manual coordinates round trip as a closed longitude-first polygon', () => {
  const polygon = manualPointsToGeoJson(points)
  assert.equal(polygon.type, 'Polygon')
  assert.equal(polygon.coordinates[0].length, 5)
  assert.deepEqual(polygon.coordinates[0][0], [106, 10])
  assert.deepEqual(polygon.coordinates[0][4], polygon.coordinates[0][0])
  assert.deepEqual(geoJsonToManualPoints(polygon), points)
  assert.ok(calculatePolygonArea(polygon) > 0)
  assert.deepEqual(calculatePolygonCentroid(polygon), { latitude: 10.5, longitude: 106.5 })
})
test('reject incomplete, out of range, degenerate and intersecting boundaries', () => {
  for (const invalid of [
    [{ latitude: '', longitude: 106 }, ...points.slice(1)],
    [{ latitude: 91, longitude: 106 }, ...points.slice(1)],
    [{ latitude: 10, longitude: 181 }, ...points.slice(1)],
    [points[0], points[0], points[1]],
    [points[0], points[2], points[1], points[3]],
    [{ latitude: 0, longitude: 0 }, { latitude: 0, longitude: 1 }, { latitude: 0, longitude: 2 }],
  ]) assert.throws(() => manualPointsToGeoJson(invalid))
  assert.throws(() => normalizePolygonGeoJson({ type: 'Point', coordinates: [1, 2] }))
})
test('containment checks include edges, not only vertices', () => {
  const farm = manualPointsToGeoJson(points)
  const inside = manualPointsToGeoJson(points.map(p => ({
    latitude: 10.25 + (p.latitude - 10) / 2, longitude: 106.25 + (p.longitude - 106) / 2,
  })))
  const outside = manualPointsToGeoJson(points.map(p => ({ ...p, longitude: p.longitude + 2 })))
  assert.equal(isPolygonInsidePolygon(inside, farm), true)
  assert.equal(isPolygonInsidePolygon(outside, farm), false)
  assert.equal(isPolygonInsidePolygon(farm, farm), true)
  assert.equal(isPointInsidePolygon({ latitude: 10.5, longitude: 106.5 }, farm), true)
  const concave = { type: 'Polygon', coordinates: [[[0, 0], [4, 0], [4, 4], [3, 4], [3, 1], [1, 1], [1, 4], [0, 4], [0, 0]]] }
  const bridge = { type: 'Polygon', coordinates: [[[0.5, 0.5], [3.5, 0.5], [3.5, 3], [0.5, 3], [0.5, 0.5]]] }
  assert.equal(isPolygonInsidePolygon(bridge, concave), false)
})
