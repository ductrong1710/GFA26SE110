import { api } from './index.js'

// Catalogs are bounded by API pages; never silently discard records after page one.
export async function listAll(path, query = {}, signal, auth = true) {
  const items = []
  let page = 1
  while (true) {
    const result = await api.request(path, { query: { ...query, page, pageSize: 100 }, signal, auth })
    items.push(...result.items)
    if (page >= result.pagination.totalPages) return items
    page++
  }
}
