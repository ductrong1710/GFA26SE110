// Example device validation ranges and display precision, not crop-care advice.
export const sensorTypes = [
  { id: 1, code: 'air_temperature', name: 'Temperature', unit: '°C', precision: 1, minValue: -40, maxValue: 85 },
  { id: 2, code: 'air_humidity', name: 'Air Humidity', unit: '%', precision: 1, minValue: 0, maxValue: 100 },
  { id: 3, code: 'soil_moisture', name: 'Soil Moisture', unit: '%', precision: 1, minValue: 0, maxValue: 100 },
  { id: 4, code: 'light_intensity', name: 'Light Intensity', unit: 'lux', precision: 0, minValue: 0, maxValue: 200000 },
  { id: 5, code: 'ph', name: 'pH', unit: 'pH', precision: 2, minValue: 0, maxValue: 14 },
  { id: 6, code: 'water_level', name: 'Water Level', unit: 'cm', precision: 1, minValue: 0, maxValue: 500 },
]
