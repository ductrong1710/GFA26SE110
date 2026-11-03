using FarmMonitoring.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FarmMonitoring.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<ServicePlan> ServicePlans => Set<ServicePlan>();
    public DbSet<PublicContent> PublicContent => Set<PublicContent>();
    public DbSet<CropSeason> CropSeasons => Set<CropSeason>();
    public DbSet<CultivationActivity> CultivationActivities => Set<CultivationActivity>();
    public DbSet<CropConditionReport> CropConditionReports => Set<CropConditionReport>();
    public DbSet<CropConsultationUpdate> CropConsultationUpdates => Set<CropConsultationUpdate>();
    public DbSet<MaintenanceSchedule> MaintenanceSchedules => Set<MaintenanceSchedule>();
    public DbSet<MaintenanceOccurrence> MaintenanceOccurrences => Set<MaintenanceOccurrence>();
    public DbSet<DeviceIncident> DeviceIncidents => Set<DeviceIncident>();
    public DbSet<ServiceJob> ServiceJobs => Set<ServiceJob>();
    public DbSet<ServiceReport> ServiceReports => Set<ServiceReport>();
    public DbSet<ServiceDeviceReplacement> ServiceDeviceReplacements => Set<ServiceDeviceReplacement>();
    public DbSet<Consultation> Consultations => Set<Consultation>();
    public DbSet<FarmSurvey> FarmSurveys => Set<FarmSurvey>();
    public DbSet<DeviceProposal> DeviceProposals => Set<DeviceProposal>();
    public DbSet<InstallationRequest> InstallationRequests => Set<InstallationRequest>();
    public DbSet<RentalContract> RentalContracts => Set<RentalContract>();
    public DbSet<RentalDeviceAssignment> RentalDeviceAssignments => Set<RentalDeviceAssignment>();
    public DbSet<InstallationJob> InstallationJobs => Set<InstallationJob>();
    public DbSet<InstallationReport> InstallationReports => Set<InstallationReport>();
    public DbSet<InstallationDeviceAllocation> InstallationDeviceAllocations => Set<InstallationDeviceAllocation>();
    public DbSet<ArchivedSensorRecord> ArchivedSensorRecords => Set<ArchivedSensorRecord>();
    public DbSet<FlightTest> FlightTests => Set<FlightTest>();
    public DbSet<UserFarm> UserFarms => Set<UserFarm>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Farm> Farms => Set<Farm>();
    public DbSet<Zone> Zones => Set<Zone>();
    public DbSet<SensorType> SensorTypes => Set<SensorType>();
    public DbSet<SensorNode> SensorNodes => Set<SensorNode>();
    public DbSet<SensorChannel> SensorChannels => Set<SensorChannel>();
    public DbSet<Uav> Uavs => Set<Uav>();
    public DbSet<Gateway> Gateways => Set<Gateway>();
    public DbSet<GatewayCredential> GatewayCredentials => Set<GatewayCredential>();
    public DbSet<SensorThreshold> SensorThresholds => Set<SensorThreshold>();
    public DbSet<Mission> Missions => Set<Mission>();
    public DbSet<MissionTarget> MissionTargets => Set<MissionTarget>();
    public DbSet<MissionWaypoint> MissionWaypoints => Set<MissionWaypoint>();
    public DbSet<MissionLog> MissionLogs => Set<MissionLog>();
    public DbSet<TelemetryRecord> TelemetryRecords => Set<TelemetryRecord>();
    public DbSet<SensorReading> SensorReadings => Set<SensorReading>();
    public DbSet<SyncBatch> SyncBatches => Set<SyncBatch>();
    public DbSet<CollectionAttempt> CollectionAttempts => Set<CollectionAttempt>();
    public DbSet<Alert> Alerts => Set<Alert>();
    public DbSet<AlertHistory> AlertHistories => Set<AlertHistory>();
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
}
