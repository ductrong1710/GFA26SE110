// Preview only: never persisted or submitted to the API.
export const rentalDemo = [
  [1041,12,'ACTIVE','2026-10-01','2027-01-01',13500000,'UAV DJI Matrice','UAV-DEMO-001'],
  [1042,18,'DRAFT','2026-11-10','2027-02-10',3600000,'Gateway Raspberry Pi','GW-DEMO-002'],
  [1043,23,'EXPIRED','2026-08-01','2026-11-01',9000000,'UAV khảo sát','UAV-DEMO-003'],
  [1044,31,'ENDED','2026-05-01','2026-09-01',4800000,'Bộ cảm biến thời tiết','SN-DEMO-004'],
].map(([id,farmId,status,start,end,totalRentVnd,name,code]) => ({
  id,farmId,status,totalRentVnd,startAt: start+'T00:00:00+07:00',endAt: end+'T00:00:00+07:00',
  devices: [{id:id*10,name,code,installationLocation:'Khu A · nông trại #'+farmId,returnedAt:status==='ENDED'?end+'T10:00:00+07:00':null},
    ...(status==='ACTIVE'?[{id:id*10+1,name:'Gateway ESP32',code:'GW-DEMO-001',installationLocation:'Nhà điều hành',returnedAt:null},{id:id*10+2,name:'Cảm biến độ ẩm đất',code:'SN-DEMO-001',installationLocation:'Khu A · luống số 3',returnedAt:null}]:[])],
  history: [{id:1,action:'CREATED',note:'Hồ sơ mẫu từ báo cáo lắp đặt đã duyệt. Hợp đồng giấy DEMO-'+id+'.',createdAt:start+'T00:00:00+07:00'},
    ...(status!=='DRAFT'?[{id:2,action:'ACTIVATED',note:'Kích hoạt kỳ thuê theo giá đã chốt.',createdAt:start+'T00:00:00+07:00'}]:[]),
    ...(status==='ENDED'?[{id:3,action:'ENDED',note:'Đã nhận lại toàn bộ thiết bị và kết thúc hồ sơ.',createdAt:end+'T10:00:00+07:00'}]:[])],
}))
