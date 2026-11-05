import { useState } from 'react'
import { useApiQuery } from './useApiQuery'
import { parseAnalyticsTime, recentAnalyticsRange, validateAnalyticsRange } from './analytics'

export const READING_REFRESH_MS = 5000

export function useReadingQuery(key, fixedRange, load, enabled) {
  const [initialRange] = useState(() => recentAnalyticsRange(Date.now(), true))
  const query = useApiQuery(`${key}:${fixedRange ? fixedRange.from + '/' + fixedRange.to : 'live'}`, async signal => {
    const range = fixedRange ?? recentAnalyticsRange(Date.now(), true)
    const error = validateAnalyticsRange(range)
    if (error) throw new Error(error)
    const bounds = { from: new Date(parseAnalyticsTime(range.from)).toISOString(), to: new Date(parseAnalyticsTime(range.to)).toISOString() }
    return { range, rows: await load(bounds, signal) }
  }, enabled, fixedRange && validateAnalyticsRange(fixedRange) ? 0 : READING_REFRESH_MS)
  return { ...query, data: query.data?.rows, range: query.data?.range ?? fixedRange ?? initialRange }
}
