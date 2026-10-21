import { area, centroid, polygon, kinks, difference, featureCollection, booleanPointInPolygon, point } from '@turf/turf'

const numeric = value => value !== null && value !== undefined && String(value).trim() !== '' && Number.isFinite(Number(value))
export const validateLatitude = value => numeric(value) && Number(value) >= -90 && Number(value) <= 90
export const validateLongitude = value => numeric(value) && Number(value) >= -180 && Number(value) <= 180
const equal = (a, b) => a[0] === b[0] && a[1] === b[1]

// A single exterior ring for the MVP; never silently discard holes or extra dimensions.
export function normalizePolygonGeoJson(value) {
  if (value?.type !== 'Polygon' || !Array.isArray(value.coordinates) || value.coordinates.length !== 1 ||
      !Array.isArray(value.coordinates[0])) throw new Error('Boundary must be a Polygon with one exterior ring.')
  const ring = value.coordinates[0].map(pair => {
    if (!Array.isArray(pair) || pair.length !== 2 || !pair.every(n => typeof n === 'number') ||
        !validateLongitude(pair[0]) || !validateLatitude(pair[1])) throw new Error('Boundary coordinates must contain valid longitude and latitude.')
    return [...pair]
  })
  if (ring.length > 1 && equal(ring[0], ring.at(-1))) ring.pop()
  if (ring.length < 3 || new Set(ring.map(p => p.join(','))).size !== ring.length)
    throw new Error('Boundary needs at least three distinct vertices, without repeated corners.')
  ring.push([...ring[0]])
  const feature = polygon([ring])
  if (kinks(feature).features.length || !(area(feature) > 0)) throw new Error('Boundary must have non-zero area and must not intersect itself.')
  return feature.geometry
}

export function manualPointsToGeoJson(points) {
  const coordinates = points.map(({ latitude, longitude }) => {
    if (!validateLatitude(latitude) || !validateLongitude(longitude))
      throw new Error('Complete every point: latitude must be -90 to 90 and longitude -180 to 180.')
    return [Number(longitude), Number(latitude)]
  })
  return normalizePolygonGeoJson({ type: 'Polygon', coordinates: [coordinates] })
}
export const geoJsonToManualPoints = value => normalizePolygonGeoJson(value).coordinates[0].slice(0, -1)
  .map(([longitude, latitude]) => ({ latitude, longitude }))
export const calculatePolygonArea = value => area(normalizePolygonGeoJson(value))
export function calculatePolygonCentroid(value) {
  const [longitude, latitude] = centroid(normalizePolygonGeoJson(value)).geometry.coordinates
  return { latitude, longitude }
}
export function isPolygonInsidePolygon(child, parent) {
  // Difference also catches edges crossing a concave parent with all vertices inside.
  return difference(featureCollection([polygon(normalizePolygonGeoJson(child).coordinates),
    polygon(normalizePolygonGeoJson(parent).coordinates)])) === null
}
export function isPointInsidePolygon(value, boundary) {
  return validateLatitude(value.latitude) && validateLongitude(value.longitude) &&
    booleanPointInPolygon(point([Number(value.longitude), Number(value.latitude)]), normalizePolygonGeoJson(boundary))
}
