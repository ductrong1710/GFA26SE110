using FarmMonitoring.Application.Common;
using FluentValidation;

namespace FarmMonitoring.Application.Features.TechnicalSupport;
public sealed record PlanPriceLine(int PublicContentId, int Quantity, bool Warranty, string Reason);
public sealed record PlanPriceSnapshot(int PublicContentId, int PriceRevision, string Name, int Quantity, decimal UnitPriceVnd, bool Warranty, string Reason)
{
    public decimal TotalVnd => Warranty ? 0 : UnitPriceVnd * Quantity;
}
public sealed record ProposeServicePlan(int ExpectedRevision, string Note, PlanPriceLine[] Lines,
    int? SensorNodeId = null, int? UavId = null, int? GatewayId = null);
public sealed record ReviewServicePlan(int ExpectedRevision, bool Approve, string Note);
public sealed record ExecuteServicePlan(int ExpectedRevision, EvidenceImageData[] SignedAgreement,
    EvidenceImageData[]? OldDeviceImages = null, EvidenceImageData[]? NewDeviceImages = null);
public sealed record PaperInstallation(EvidenceImageData[] Contract, EvidenceImageData[] Handover,
    EvidenceImageData[]? Bills = null, string? PaperContractNumber = null);
public static class PaperEvidence
{
    public static void Required(EvidenceImageData[]? images, string name)
    {
        if (images is not { Length: > 0 }) throw new ValidationException($"Attach signed paper evidence: {name}.");
        EvidenceImages.Decode(images);
    }
    public static void Installation(PaperInstallation paper)
    {
        Required(paper.Contract, "contract"); Required(paper.Handover, "handover");
        EvidenceImages.Decode(paper.Bills);
        if (paper.PaperContractNumber?.Length > 100) throw new ValidationException("Paper contract number is too long.");
    }
}
