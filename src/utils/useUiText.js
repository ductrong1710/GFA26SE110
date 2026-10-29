import { ROLES } from '../config/roles'
import { useContext } from 'react'
import { AuthContext } from '../context/AuthContext'

const commonTranslations = Object.freeze({
  'Loading…': 'Đang tải…',
  'Loading data…': 'Đang tải dữ liệu…',
  'Try again': 'Thử lại',
  'Retry': 'Thử lại',
  'No results': 'Không có kết quả',
  'No records found': 'Không tìm thấy dữ liệu',
  'Unable to load data': 'Không thể tải dữ liệu',
  'Try adjusting your filters or check back later.': 'Hãy điều chỉnh bộ lọc hoặc quay lại sau.',
  'Data table': 'Bảng dữ liệu',
  'scroll area': 'khu vực cuộn',
  'Yes': 'Có',
  'No': 'Không',
  'Skip to content': 'Bỏ qua để đến nội dung',
  'Breadcrumb': 'Đường dẫn điều hướng',
})

export function useUiText(translations = {}) {
  const auth = useContext(AuthContext)
  const activeRole = auth?.activeRole
  return text => activeRole === ROLES.FARM_OWNER ? translations[text] ?? commonTranslations[text] ?? text : text
}
