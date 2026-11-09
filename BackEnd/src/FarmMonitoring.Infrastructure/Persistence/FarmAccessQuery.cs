using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using FarmMonitoring.Application.Features.Farms;
using FarmMonitoring.Domain.Constants;
using FarmMonitoring.Domain.Entities;

namespace FarmMonitoring.Infrastructure.Persistence;

internal static class FarmAccessQuery
{
    public static IQueryable<Mission> VisibleMissions(AppDbContext db, FarmAccessScope scope)
    {
        var missions = db.Missions.AsNoTracking();
        if (scope.IsAdministrator) return missions;
        var farms = AssignedFarmIds(db, scope.UserId);
        return missions.Where(x => x.CreatedByUserId == scope.UserId && farms.Contains(x.FarmId));
    }

    public static IQueryable<RentalDeviceAssignment> RentalAssignments(AppDbContext db, FarmAccessScope scope)
    {
        var farms = AssignedFarmIds(db, scope.UserId);
        return db.RentalDeviceAssignments.Where(x => x.ReturnedAt == null && x.Contract.Status != RentalStatus.ENDED &&
            (scope.IsAdministrator || x.Contract.OwnerId == scope.UserId && farms.Contains(x.Contract.FarmId)));
    }

    public static IQueryable<int> AssignedFarmIds(AppDbContext db, int userId) => db.UserFarms.Where(x =>
        x.UserId == userId && x.User.IsActive && x.User.UserRoles.Any(r =>
            r.Role.Name == RoleNames.FarmOwner)).Select(x => x.FarmId);

    // Compose an expression tree so membership restriction is translated into SQL before pagination/aggregation.
    public static IQueryable<T> ForFarms<T>(this IQueryable<T> rows, AppDbContext db, FarmAccessScope scope, Expression<Func<T, int?>> farm)
    {
        if (scope.IsAdministrator) return rows;
        var ids = AssignedFarmIds(db, scope.UserId);
        var present = Expression.Property(farm.Body, nameof(Nullable<int>.HasValue));
        var value = Expression.Property(farm.Body, nameof(Nullable<int>.Value));
        var contains = Expression.Call(typeof(Queryable), nameof(Queryable.Contains), [typeof(int)], ids.Expression, value);
        return rows.Where(Expression.Lambda<Func<T, bool>>(Expression.AndAlso(present, contains), farm.Parameters));
    }

    // All alert queries and resource checks use the same deterministic association.
    public static readonly Expression<Func<Alert, int?>> AlertFarm = x => x.SensorChannel != null
        ? x.SensorChannel.SensorNode.Zone.FarmId : x.SensorNode != null ? x.SensorNode.Zone.FarmId
        : x.Mission != null ? x.Mission.FarmId : null;
}
