import { sensorNodes } from './sensorNodes.js'
import { sensorTypes } from './sensorTypes.js'

const zoneTypes = { 1: [1, 3, 5], 2: [1, 2, 4], 3: [1, 3, 6], 4: [1, 3, 5] }
const thresholds = { 1: [18, 35], 2: [45, 90], 3: [25, 65], 4: [5000, 80000], 5: [5.5, 7.5], 6: [45, 160] }

export const sensorChannels = sensorNodes.flatMap((node) => zoneTypes[node.zoneId].map((sensorTypeId) => {
  const type = sensorTypes.find(({ id }) => id === sensorTypeId)
  return {
    id: node.id * 100 + sensorTypeId, sensorNodeId: node.id, sensorTypeId,
    channelCode: `${node.deviceCode}:${type.code}`, name: type.name, isActive: true,
    warningMin: thresholds[sensorTypeId][0], warningMax: thresholds[sensorTypeId][1],
    dataTimeoutMinutes: 90, sampleIntervalSeconds: 300,
  }
}))
