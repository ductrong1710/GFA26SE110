import { useEffect, useState } from 'react'
import { useAuth } from './AuthContext'
import { OperationsContext } from './OperationsContext'
import { createOperationsState, changeOperationsState } from '../data/mock/operationsState'
import { emptyMissionPlan, saveMissionPlan } from '../data/mock/missionPlanning'
import { updateMissionMonitoring } from '../data/mock/missionMonitoring'
import { beginSyncRetry, finishSyncRetry } from '../data/mock/syncState'

export function OperationsProvider({ children }) {
  const { user, activeRole } = useAuth()
  const [operations, setOperations] = useState(createOperationsState)
  // Demo-only completion timer lives above routes so navigating away does not cancel a retry.
  useEffect(() => {
    const timers = operations.syncBatches.filter((batch) => batch.activeRetry).map((batch) => setTimeout(() => {
      setOperations((current) => finishSyncRetry(current, batch.id, batch.retryCount))
    }, 1800))
    return () => timers.forEach(clearTimeout)
  }, [operations.syncBatches])
  const retrySync = (batchId) => setOperations(beginSyncRetry(operations, batchId, { id: user.id, fullName: user.fullName, activeRole }))
  const [missionWizard, setMissionWizard] = useState(() => ({ step: 1, plan: emptyMissionPlan() }))
  const reportMission = (change, workspace) => {
    const next = updateMissionMonitoring(operations, change, { id: user.id, fullName: user.fullName, activeRole }, workspace)
    setOperations(next)
  }
  const startMissionPlan = (mission) => setMissionWizard({ step: mission?.plan?.wizardStep ?? 1, plan: mission?.plan ? structuredClone(mission.plan) : emptyMissionPlan() })
  const savePlannedMission = (workspace, draft) => {
    const plan = { ...missionWizard.plan, wizardStep: missionWizard.step }
    const next = saveMissionPlan(operations, plan, workspace, { id: user.id, activeRole }, draft)
    const id = plan.missionId ?? operations.nextMissionId
    setOperations(next)
    setMissionWizard(draft ? { ...missionWizard, plan: next.missions.find((mission) => mission.id === id).plan } : { step: 1, plan: emptyMissionPlan() })
    return id
  }
  const changeOperations = (change, workspace) => {
    const next = changeOperationsState(operations, change, workspace, { id: user.id, activeRole })
    setOperations(next)
    return next
  }
  return <OperationsContext.Provider value={{ operations, changeOperations, missionWizard, setMissionWizard, startMissionPlan, savePlannedMission, reportMission, retrySync }}>{children}</OperationsContext.Provider>
}
