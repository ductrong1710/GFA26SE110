import { useContext } from 'react'
import { AuthContext } from '../context/AuthContext'
import { ROLES } from '../config/roles'
import pageEnglish from './workflowPageEnglish'
import formEnglish from './workflowFormEnglish'
import { label, money, time } from '../services/api/workflow'

const english = { ...pageEnglish, ...formEnglish }

export function workflowText(text, admin) {
  if (!admin || typeof text !== 'string') return text
  const translated = english[text.trim()]
  return translated === undefined ? text : text.replace(text.trim(), () => translated)
}

export function useWorkflowText() {
  const auth = useContext(AuthContext)
  return text => workflowText(text, auth?.activeRole === ROLES.ADMINISTRATOR)
}

export function useWorkflowFormat() {
  const auth = useContext(AuthContext)
  const admin = auth?.activeRole === ROLES.ADMINISTRATOR
  return {
    label: value => workflowText(label(value), admin),
    money: value => admin ? value == null ? 'Price not recorded' : `${new Intl.NumberFormat('en-US').format(value)} VND` : money(value),
    time: value => admin && value ? new Intl.DateTimeFormat('en-GB', { dateStyle: 'short', timeStyle: 'short', timeZone: 'Asia/Ho_Chi_Minh' }).format(new Date(value)) : time(value),
    locale: admin ? 'en-GB' : 'vi-VN',
  }
}
