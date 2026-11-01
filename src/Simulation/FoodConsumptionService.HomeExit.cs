using System.Numerics;

namespace Simulation;

public sealed partial class FoodConsumptionService
{
    private sealed record HomeExit(string ActionId, INavigationAdapter Navigator, string RequestId);
    private readonly Dictionary<string, HomeExit> _homeExits = new(StringComparer.Ordinal);
    private readonly RetryScheduler _homeExitRetry = new();
    private long _nextHomeExit;

    // Food travel owns this detour independently of the work/rest schedule. A closed
    // door is not evidence that the selected food itself is unreachable.
    private FoodConsumptionResult? ReconcileHomeExit(EntityRecord entity, SpeciesDefinition species, long tick)
    {
        if (!entity.Needs.IsHungry || entity.Health.IsIncapacitated || !species.CanOpenDoors)
        {
            CancelHomeExit(entity.Id, completeAction: true);
            _homeExitRetry.Clear(entity.Id);
            return null;
        }
        var house = _world.Structures.FirstOrDefault(home => home.ResidentId == entity.Id
            && home.IsClosed && HouseGeometry.Contains(home, entity.Position));
        if (house is null)
        {
            CancelHomeExit(entity.Id, completeAction: true);
            _homeExitRetry.Clear(entity.Id);
            return null;
        }
        if (!_homeExits.TryGetValue(entity.Id, out var exit))
        {
            // Select actual food first. Food inside the room requires no exit.
            if (!_activeTravels.TryGetValue(entity.Id, out var food)
                || HouseGeometry.Contains(house, food.Source.Position) || !IsCurrentAction(entity, food)) return null;
            if (_homeExitRetry.IsWaiting(entity.Id, tick))
                return new(FoodConsumptionStatus.Waiting, WaitingReason: "Home door path unavailable.");
            entity.Actions.Cancel(); // Releases the original food path/reservation through its cleanup.
            var action = $"food-door-{entity.Id}-{checked(++_nextHomeExit)}";
            if (entity.Actions.TryStart(new ActionCandidate(action, ActionKind.Eat), () => CancelHomeExit(entity.Id))
                == ActionStartResult.Rejected) return null;
            var navigator = _navigationForEntity(entity);
            try
            {
                var request = navigator.Request(new NavigationRequest(
                    house.ToWorld(new Vector3(0, 0, HouseGeometry.DoorZ - 1.25f)),
                    timeoutSeconds: NavigationTimeoutSeconds, arrivalTolerance: .4));
                exit = new HomeExit(action, navigator, request);
                _homeExits.Add(entity.Id, exit);
            }
            catch
            {
                entity.Actions.Cancel();
                throw;
            }
        }
        if (entity.Actions.Current?.Id != exit.ActionId)
        {
            CancelHomeExit(entity.Id);
            return null;
        }
        if (Vector3.Distance(entity.Position, house.DoorPosition) <= 2)
        {
            var opened = _world.OperateDoor(entity.Id, house.Id, HouseDoorAction.Open);
            if (opened.Succeeded)
            {
                CancelHomeExit(entity.Id, completeAction: true);
                _homeExitRetry.Clear(entity.Id);
                return null; // Rebuild food travel from intent, without rejecting the food candidate.
            }
        }
        var outcome = exit.Navigator.Poll(exit.RequestId, 0);
        if (outcome != NavigationOutcome.InProgress)
        {
            CancelHomeExit(entity.Id, completeAction: true);
            _homeExitRetry.ScheduleRetry(entity.Id, tick);
            entity.Activity = "Waiting";
            entity.WaitingReason = "Home door path unavailable.";
            return new(FoodConsumptionStatus.Waiting, WaitingReason: entity.WaitingReason);
        }
        entity.Activity = "Opening home door for food";
        entity.WaitingReason = null;
        return new(FoodConsumptionStatus.Traveling, ActionId: exit.ActionId);
    }

    private void CancelHomeExit(string entityId, bool completeAction = false)
    {
        if (!_homeExits.Remove(entityId, out var exit)) return;
        exit.Navigator.Cancel(exit.RequestId);
        if (completeAction && _world.TryGetEntity(entityId, out var entity) && entity?.Actions.Current?.Id == exit.ActionId)
            entity.Actions.Complete();
    }

    private void ResetHomeDoorExits()
    {
        foreach (var entityId in _homeExits.Keys.ToArray()) CancelHomeExit(entityId, completeAction: true);
        _homeExitRetry.Reset();
        _nextHomeExit = 0;
    }
}
