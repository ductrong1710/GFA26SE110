namespace FarmMonitoring.Domain.Entities;

public sealed class ArchivedSensorRecord
{
    public int Id { get; set; }
    public int SensorNodeId { get; set; }
    public SensorNode SensorNode { get; set; } = null!;
    public int GatewayId { get; set; }
    public Gateway Gateway { get; set; } = null!;
    public int? MissionId { get; set; }
    public Mission? Mission { get; set; }
    public string SourceRecordKey { get; set; } = "";
    public string PayloadJson { get; set; } = "";
    public DateTimeOffset ReceivedAt { get; set; }
}
