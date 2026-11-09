# Mission file inventory

Created by this implementation:

- docs/MISSION_IMPLEMENTATION.md
- docs/MISSION_OPERATIONS.md
- include/FlightSafetyController.h
- include/MissionBackendClient.h
- include/MissionCollection.h
- include/MissionManager.h
- include/MissionModel.h
- include/MissionRoutePlanner.h
- include/MissionStorage.h
- src/FlightSafetyController.cpp
- src/MissionBackendClient.cpp
- src/MissionHttpTransport.cpp
- src/MissionLittleFS.cpp
- src/MissionManager.cpp
- src/MissionModel.cpp
- src/MissionRoutePlanner.cpp
- src/MissionStorage.cpp
- tests/host/FS.h
- tests/host/MissionMemoryFiles.h
- tests/host/mission_backend_tests.cpp
- tests/host/mission_collection_tests.cpp
- tests/host/mission_end_to_end_tests.cpp
- tests/host/mission_manager_tests.cpp
- tests/host/mission_planner_tests.cpp
- tests/host/mission_storage_tests.cpp
- tests/host/mission_sync_tests.cpp
- tests/host/tello_safety_tests.cpp
- tests/run_mission_host.ps1
- tests/test_mock_mission.py
- tools/fixtures/mission-collect.json
- tools/fixtures/mission-short.json
- tools/mock_mission_backend.py
- docs/MISSION_FILES.md

Modified by this implementation:

- README.md
- include/ApiServer.h
- include/CollectionManager.h
- include/Config.h
- include/FlightStateManager.h
- include/GatewayWiFiManager.h
- include/TelloController.h
- src/ApiServer.cpp
- src/CollectionManager.cpp
- src/FlightStateManager.cpp
- src/GatewayWiFiManager.cpp
- src/TelloController.cpp
- src/main.cpp
- tests/host/Arduino.h
- tests/host/network_tests.cpp
- tests/host/tello_tests.cpp
- tests/run_tello_host.ps1

Pre-existing prompt changes were left unchanged: the old Tello prompt deletion and the new mission prompt file. No backend files or Secrets.h were changed.
