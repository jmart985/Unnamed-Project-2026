using System.Numerics;
using System.Text;
using Simulation;

namespace Simulation.Tests;

public sealed class PlayerFoodConsumptionServiceTests
{
    [Fact]
    public void EatNearbyConsumesOneCompatibleUnitAndClampsNutrition()
    {
        var (session, service, source) = CreateSession();
        Assert.True(session.World.TrySetEntityPosition(session.Player.Id, source.Position));
        session.Player.Needs.Advance(200);
        var originalCount = source.Contents.Counts[ItemIds.Food];
        var originalRemaining = service.FoodUnitsRemaining;

        var result = service.EatNearby();

        Assert.Equal(PlayerFoodConsumptionStatus.Ate, result.Status);
        Assert.Same(source, result.Source);
        Assert.Equal(originalCount - 1, source.Contents.Counts[ItemIds.Food]);
        Assert.Equal(70, session.Player.Satiety);
        Assert.Equal(1, service.FoodUnitsConsumed);
        Assert.Equal(originalRemaining - 1, service.FoodUnitsRemaining);

        Assert.Equal(PlayerFoodConsumptionStatus.Ate, service.EatNearby().Status);
        Assert.Equal(100, session.Player.Satiety);
        Assert.Equal(2, service.FoodUnitsConsumed);
        Assert.Equal(originalCount - 2, source.Contents.Counts[ItemIds.Food]);
    }

    [Fact]
    public void EatNearbyAcceptsTheExactInteractionRangeAndRejectsBeyondItWithoutMutation()
    {
        var (session, service, source) = CreateSession();
        var exactRangePosition = source.Position + new Vector3((float)PlayerFoodConsumptionService.DefaultMaximumReach, 0, 0);
        Assert.True(session.World.TrySetEntityPosition(session.Player.Id, exactRangePosition));

        Assert.Equal(PlayerFoodConsumptionStatus.Ate, service.EatNearby().Status);
        var satietyAfterSuccess = session.Player.Satiety;
        var remainingAfterSuccess = service.FoodUnitsRemaining;

        Assert.True(session.World.TrySetEntityPosition(session.Player.Id, new Vector3(200, 0, 200)));
        var outOfRange = service.EatNearby();

        Assert.Equal(PlayerFoodConsumptionStatus.OutOfRange, outOfRange.Status);
        Assert.Equal(satietyAfterSuccess, session.Player.Satiety);
        Assert.Equal(remainingAfterSuccess, service.FoodUnitsRemaining);
        Assert.Equal(1, service.FoodUnitsConsumed);
    }

    [Fact]
    public void EatNearbyRejectsEmptyAndIncompatibleFoodWithoutMutation()
    {
        var (session, service, _) = CreateSession();
        foreach (var source in session.World.FoodSources)
        {
            RemoveAllContents(source.Contents);
        }

        Assert.Equal(PlayerFoodConsumptionStatus.NoFoodAvailable, service.EatNearby().Status);
        Assert.Equal(0, service.FoodUnitsRemaining);
        Assert.Equal(60, session.Player.Satiety);

        var restrictedCatalog = GetSpeciesCatalog(restrictHumanToMeat: true);
        var restrictedSession = NewGameSession.Create(
            restrictedCatalog,
            PlayerCharacterProfile.Create("Test Player"),
            seed: 2026);
        foreach (var source in restrictedSession.World.FoodSources.ToArray())
        {
            Assert.True(restrictedSession.World.TryRemoveFoodSource(source.Id));
        }

        var plantSource = restrictedSession.World.AddFoodSource(
            "plant-only",
            Vector3.Zero,
            CreateContents("plant-only", ItemIds.Plant, 1));
        Assert.True(restrictedSession.World.TrySetEntityPosition(restrictedSession.Player.Id, plantSource.Position));
        var restrictedService = new PlayerFoodConsumptionService(restrictedSession.World, restrictedCatalog);

        Assert.Equal(PlayerFoodConsumptionStatus.IncompatibleFood, restrictedService.EatNearby().Status);
        Assert.Equal(1, plantSource.Contents.Counts[ItemIds.Plant]);
        Assert.Equal(60, restrictedSession.Player.Satiety);
        Assert.Equal(0, restrictedService.FoodUnitsConsumed);
    }

    [Fact]
    public void EatNearbyDoesNotConsumeFoodReservedByAnotherAction()
    {
        var (session, service, source) = CreateSession();
        Assert.True(session.World.TrySetEntityPosition(session.Player.Id, source.Position));
        var reserver = session.World.Entities.First(entity => entity.Id != session.Player.Id);
        const string actionId = "reserve-food-for-test";
        Assert.NotEqual(ActionStartResult.Rejected,
            reserver.Actions.TryStart(new ActionCandidate(actionId, ActionKind.Eat)));
        Assert.Equal(
            ReservationResult.Reserved,
            session.World.Reservations.TryReserveResource(
                reserver.Id,
                actionId,
                source.Id,
                source.Contents,
                ItemIds.Food,
                source.Contents.Counts[ItemIds.Food]));

        var result = service.EatNearby();

        Assert.Equal(PlayerFoodConsumptionStatus.FoodUnavailable, result.Status);
        Assert.Equal(100, source.Contents.Counts[ItemIds.Food]);
        Assert.Equal(60, session.Player.Satiety);
        Assert.Equal(0, service.FoodUnitsConsumed);
    }

    [Fact]
    public void PlayerEatCommandReturnsAnObservableSuccessOrRejection()
    {
        var (session, service, source) = CreateSession();
        Assert.True(session.World.TrySetEntityPosition(session.Player.Id, source.Position));
        var commands = new PlayerCommandBoundary(
            session.World,
            new SimulationClock(),
            playerFoodConsumption: service);

        var success = commands.Execute(new PlayerCommand(PlayerCommandKind.Eat));

        Assert.Equal(PlayerCommandResultCode.Accepted, success.Code);
        Assert.Equal(source.Id, success.EntityId);
        Assert.Equal(1, service.FoodUnitsConsumed);

        Assert.True(session.World.TrySetEntityPosition(session.Player.Id, new Vector3(200, 0, 200)));
        var rejection = commands.Execute(new PlayerCommand(PlayerCommandKind.Eat));

        Assert.Equal(PlayerCommandResultCode.FoodRejected, rejection.Code);
        Assert.Contains("closer", rejection.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, service.FoodUnitsConsumed);
    }

    [Fact]
    public void EatNearbyRejectsWhenTheSessionPlayerIsMissing()
    {
        var (session, service, _) = CreateSession();
        Assert.True(session.World.TryRemoveEntity(session.Player.Id));

        Assert.Equal(PlayerFoodConsumptionStatus.PlayerUnavailable, service.EatNearby().Status);
        Assert.Equal(0, service.FoodUnitsConsumed);
    }

    private static (NewGameSession Session, PlayerFoodConsumptionService Service, FoodSource Source) CreateSession()
    {
        var catalog = GetSpeciesCatalog();
        var session = NewGameSession.Create(
            catalog,
            PlayerCharacterProfile.Create("Test Player"),
            seed: 2026);
        var source = session.World.FoodSources.Single(candidate =>
            candidate.Contents.Counts.ContainsKey(ItemIds.Food));
        var service = new PlayerFoodConsumptionService(session.World, catalog);
        return (session, service, source);
    }

    private static SpeciesCatalog GetSpeciesCatalog(bool restrictHumanToMeat = false)
    {
        var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "species.json"));
        if (restrictHumanToMeat)
        {
            json = json.Replace(
                "\"diet\": [\"food\", \"plant\", \"fruit\", \"meat\"]",
                "\"diet\": [\"meat\"]",
                StringComparison.Ordinal);
        }

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        return SpeciesCatalog.Load(stream);
    }

    private static ResourceContainer CreateContents(string id, string itemId, int count)
    {
        var contents = new ResourceContainer(id, count);
        Assert.Equal(ResourceMutationResult.Success, contents.TryAdd(itemId, count));
        return contents;
    }

    private static void RemoveAllContents(ResourceContainer contents)
    {
        foreach (var (itemId, count) in contents.Counts.ToArray())
        {
            Assert.Equal(ResourceMutationResult.Success, contents.TryRemove(itemId, count));
        }
    }
}
