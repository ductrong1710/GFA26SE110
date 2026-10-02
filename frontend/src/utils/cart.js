const CART_KEY = 'smartfarm-cart'
const CART_EVENT = 'smartfarm-cart-change'

export function getCartItems() {
  try {
    return JSON.parse(window.localStorage.getItem(CART_KEY) || '[]')
  } catch {
    return []
  }
}

function notifyCartChange() {
  window.dispatchEvent(new Event(CART_EVENT))
}

export function addRentalToCart(item) {
  const items = getCartItems()
  const key = item.productId + ':' + item.duration
  const existing = items.find((entry) => entry.key === key)
  const quantity = Math.max(1, Number(item.quantity) || 1)
  if (existing) existing.quantity += quantity
  else items.push({ ...item, key, quantity })
  window.localStorage.setItem(CART_KEY, JSON.stringify(items))
  notifyCartChange()
}

export function updateCartQuantity(key, quantity) {
  const items = getCartItems().map((item) => item.key === key ? { ...item, quantity: Math.max(1, Number(quantity) || 1) } : item)
  window.localStorage.setItem(CART_KEY, JSON.stringify(items))
  notifyCartChange()
}

export function removeCartItem(key) {
  const items = getCartItems().filter((item) => item.key !== key)
  window.localStorage.setItem(CART_KEY, JSON.stringify(items))
  notifyCartChange()
}

export function subscribeToCart(callback) {
  window.addEventListener(CART_EVENT, callback)
  window.addEventListener('storage', callback)
  return () => {
    window.removeEventListener(CART_EVENT, callback)
    window.removeEventListener('storage', callback)
  }
}

export function getCartCount(items = getCartItems()) {
  return items.reduce((total, item) => total + item.quantity, 0)
}
