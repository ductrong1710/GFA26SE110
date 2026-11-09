import { api } from './index.js'

// Catalogs are bounded by API pages; never silently discard records after page one.
export async function listAll(path, query = {}, signal) {
  const items = []
  let page = 1
  while (true) {
    const result = await api.get(path, { ...query, page, pageSize: 100 }, signal)
    items.push(...result.items)
    if (page >= result.pagination.totalPages) return items
    page++
  }
}
