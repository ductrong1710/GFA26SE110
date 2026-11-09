import { toast } from 'react-toastify'

export function showToast(type, message) {
  if (!message) return
  toast[type](message)
}
