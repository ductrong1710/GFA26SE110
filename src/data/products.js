export const exampleBasePrice = 33600000

const products = [
  { id: 'soil-moisture', category: 'sensor', icon: 'droplet', tag: 'ĐẤT & NƯỚC', name: 'Cảm biến độ ẩm đất', description: 'Theo dõi độ ẩm tại vùng rễ để chủ động điều chỉnh lịch tưới.', specs: ['Độ ẩm đất', 'Nhiệt độ đất', 'Truyền dữ liệu không dây'], image: '/images/sensors/soil-moisture.svg' },
  { id: 'soil-ph', category: 'sensor', icon: 'sensor', tag: 'CHẤT LƯỢNG ĐẤT', name: 'Cảm biến pH đất', description: 'Đo độ pH đất, hỗ trợ đánh giá môi trường canh tác theo từng khu vực.', specs: ['Đo pH tại chỗ', 'Thiết kế chống nước', 'Lắp đặt ngoài đồng'], image: '/images/sensors/soil-ph.svg' },
  { id: 'light', category: 'sensor', icon: 'sun', tag: 'MÔI TRƯỜNG', name: 'Cảm biến ánh sáng', description: 'Ghi nhận cường độ ánh sáng tại vườn hoặc nhà lưới trong ngày.', specs: ['Cường độ ánh sáng', 'Theo dõi liên tục', 'Tổng hợp theo thời gian'], image: '/images/sensors/air-light.svg' },
  { id: 'air-humidity', category: 'sensor', icon: 'wind', tag: 'KHÔNG KHÍ', name: 'Cảm biến độ ẩm không khí', description: 'Theo dõi độ ẩm và nhiệt độ không khí cho vùng trồng, nhà kính.', specs: ['Độ ẩm không khí', 'Nhiệt độ môi trường', 'Cảnh báo theo ngưỡng'], image: '/images/sensors/air-light.svg' },
  { id: 'uav-gateway', category: 'uav', icon: 'drone', tag: 'THU THẬP DỮ LIỆU', name: 'UAV & gateway di động', description: 'Kết hợp nhiệm vụ bay với gateway để thu thập dữ liệu từ các cảm biến trong vùng trồng.', specs: ['Lập tuyến waypoint', 'Kết nối cảm biến', 'Đồng bộ dữ liệu'], image: 'https://images.unsplash.com/photo-1473968512647-3e447244af8f?auto=format&fit=crop&w=900&q=80' },
]

export default products
