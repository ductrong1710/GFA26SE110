# Raspberry Pi gateway — GFA26SE110

Nhánh `RaspberryPi` chỉ chứa mã nguồn gateway Raspberry Pi, tách riêng khỏi các nhánh `BackEnd`, `FrontEnd`, `ESP32` và `ESP6288`.

Code, cấu hình mẫu, test mô phỏng và hướng dẫn triển khai nằm trong [raspberry-pi-gateway](raspberry-pi-gateway/README.md).

```sh
git clone --branch RaspberryPi --single-branch https://github.com/ductrong1710/GFA26SE110.git raspberry-pi
cd raspberry-pi/raspberry-pi-gateway
```

Nguồn được giữ nguyên từ commit `9a494fd` trên nhánh local `feature/raspberry-pi-gateway`; chỉ cập nhật tài liệu nhánh và quy tắc bỏ qua file riêng tư khi tách. Không đưa cache, bản build, database hay credentials vào Git.

Gateway mặc định chạy ground/simulation mode. Xem [giới hạn và nghiệm thu còn chờ](raspberry-pi-gateway/docs/verification.md) trước khi dùng phần cứng. Chưa nghiệm thu Pi/ESP8266/Tello thật.
