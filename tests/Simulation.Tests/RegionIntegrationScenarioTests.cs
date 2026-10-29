using System.Numerics;
using System.Text.Json.Nodes;
using Simulation;

namespace Simulation.Tests;

public sealed class RegionIntegrationScenarioTests
{
    [Fact]
    public void OneRegionComposesCoreSystemsAndRestoresOnlyDurableState()
    {
        Assert.Equal(RunScenario(), RunScenario());
    }

    private static string RunScenario()
    {
        const int seed = 42;
        var species = SpeciesCatalog.LoadFromFile(Data("species.json"));
        var goods = TradeGoodsCatalog.LoadFromFile(Data("trade_goods.json"));
        var events = RelationshipEventCatalog.LoadFromFile(Data("relationship_events.json"));
        var world = new World();
        world.SpeciesCatalog = species;
        var human = world.SpawnEntity(species.GetRequired("human"), Vector3.Zero);
        var creature = world.SpawnEntity(species.GetRequired("creature"), Vector3.UnitX, satiety: 70);
        Assert.Equal(FactionDefinitionResult.Defined, world.DefineFaction("settlers"));
        Assert.Equal(FactionDefinitionResult.Defined, world.DefineFaction("visitors"));
        Assert.Equal(FactionAllegianceResult.Assigned, world.SetEntityAllegiance(human.Id, "settlers"));
        Assert.Equal(FactionReputationResult.Applied, world.AdjustFactionReputation(human.Id, "visitors", 35));
        Assert.Equal(FactionHostilityResult.Updated, world.SetFactionHostility("settlers", "visitors", true));
        Assert.Equal(FactionHostilityResult.Updated, world.SetFactionHostility("visitors", "settlers", false));
        Assert.Equal(RelationshipAdjustmentResult.Applied, world.ApplyRelationshipAdjustment(
            creature.Id, human.Id, new RelationshipAdjustment(5, 30, 0, 25)));

        var combat = new SocialMeleeCombatService(world, events);
        var hit = combat.Resolve(new MeleeAttackRequest(human.Id, creature.Id, "Torso", 10), maximumReach: 2);
        Assert.Equal(MeleeAttackResult.Success, hit.Combat.Result);
        Assert.Equal(RelationshipEventApplyResult.Applied, hit.RelationshipEvent);
        var damagedHealth = creature.Health.Parts.Single(part => part.Name == "Torso").CurrentHealth;
        Assert.Equal(creature.Health.Parts.Single(part => part.Name == "Torso").MaximumHealth - 10, damagedHealth);
        Assert.Equal(CreatureCaptureResult.Captured, world.TryCaptureCreature(creature.Id));
        Assert.Equal(CreatureLifecycleTransitionResult.Transitioned,
            world.TryTransitionCreatureState(creature.Id, CreatureCaptivityState.Captive));

        var settlement = new ResourceContainer("market", 40);
        world.AddStockpile("market", Vector3.Zero, settlement, "settlers");
        Add(settlement, ItemIds.Food, 6);
        Add(settlement, ItemIds.Coin, 10);
        var cargo = new ResourceContainer("caravan", 13);
        Add(cargo, ItemIds.Food, 1);
        Add(cargo, ItemIds.Coin, 12);
        var visits = new CaravanVisitService(new SettlementMarket(world, goods, "market"));
        var visit = visits.TryOpen("region-visit", cargo).Visit;
        Assert.NotNull(visit);
        Assert.Equal(CaravanVisitStatus.Success, visits.TryArrive(visit).Status);

        // Reuse the accepted finite 0.8 market flow; production is verified by E08d.
        var trade = visits.TryTrade(visit, MarketTradeSide.Purchase, ItemIds.Food, 3);
        Assert.Equal(CaravanVisitStatus.Success, trade.Status);
        Assert.Equal(new MarketTransactionResult(MarketTransactionStatus.Success, 3, 6), trade.Transaction);
        Assert.Equal((3, 16, 4, 6), Totals());
        AssertCoreState();

        var navigation = new FakeNavigationAdapter();
        var wandering = new WanderingService(world, _ => navigation,
            [new Vector3(2, 0, 0), new Vector3(5, 0, 0)], decisionSeed: seed);
        var clock = new SimulationClock();
        WanderingResult? trip = null;
        Assert.Equal(1, clock.Advance(0.1, elapsed =>
        {
            human.Needs.Advance(elapsed, satietyOverride: species.GetRequired("human").HungerRate);
            creature.Needs.Advance(elapsed, satietyOverride: species.GetRequired("creature").HungerRate);
            trip = wandering.Advance(creature, species.GetRequired("creature"), simulationTick: 1);
        }));
        Assert.Equal(WanderingStatus.Traveling, trip!.Status);
        Assert.NotNull(creature.Actions.Current);
        Assert.Equal(NavigationOutcome.InProgress, Assert.Single(navigation.Requests).Value);

        var beforeRejected = State();
        var rejection = visits.TryTrade(visit, MarketTradeSide.Purchase, ItemIds.Food, 4);
        Assert.Equal(CaravanVisitStatus.TradeRejected, rejection.Status);
        Assert.Equal(MarketTransactionStatus.InsufficientMarketGoods, rejection.Transaction!.Value.Status);
        Assert.Equal(beforeRejected, State());
        Assert.Equal(FactionReputationResult.UnknownFaction,
            world.AdjustFactionReputation(human.Id, "missing", 1));
        Assert.Equal(beforeRejected, State());
        Assert.Equal(FactionOwnershipResult.UnknownFaction, world.SetStockpileFactionOwner("market", "missing"));
        Assert.Equal(beforeRejected, State());
        var rejectedHit = combat.Resolve(new MeleeAttackRequest(human.Id, creature.Id, "Torso", 0), maximumReach: 2);
        Assert.Equal(MeleeAttackResult.InvalidRequest, rejectedHit.Combat.Result);
        Assert.Null(rejectedHit.RelationshipEvent);
        Assert.Equal(damagedHealth, creature.Health.Parts.Single(part => part.Name == "Torso").CurrentHealth);
        Assert.Equal(beforeRejected, State());

        // Save at a paused tick boundary with an active transient visit and trip.
        clock.SetPaused(true);
        Assert.Equal(0, clock.Advance(1));
        var snapshot = WorldSnapshotSerializer.Serialize(world, clock.SimulationTime);
        var invalid = JsonNode.Parse(snapshot)!.AsObject();
        invalid["Stockpiles"]![0]!["FactionOwnerId"] = "missing";
        Assert.Throws<InvalidDataException>(() => WorldSnapshotSerializer.Load(
            world, invalid.ToJsonString(), species, visits, wandering));
        Assert.Same(human, world.Entities.Single(entity => entity.Id == human.Id));
        Assert.Same(creature, world.Entities.Single(entity => entity.Id == creature.Id));
        Assert.Equal(beforeRejected, State());
        Assert.Equal(CaravanVisitState.Arrived, visit!.State);
        Assert.NotNull(creature.Actions.Current);
        Assert.Equal(NavigationOutcome.InProgress, Assert.Single(navigation.Requests).Value);

        var time = WorldSnapshotSerializer.Load(world, snapshot, species, visits, wandering);
        clock.Restore(time);
        Assert.Equal(0.1, time, precision: 6);
        Assert.True(clock.IsPaused);
        Assert.Equal(snapshot, WorldSnapshotSerializer.Serialize(world, time));
        Assert.Equal(beforeRejected, State());
        AssertCoreState();
        Assert.Equal((3, 16, 4, 6), Totals());
        Assert.All(world.Entities, entity => Assert.Null(entity.Actions.Current));
        Assert.Equal(NavigationOutcome.Cancelled, Assert.Single(navigation.Requests).Value);
        Assert.Equal(CaravanVisitState.Cancelled, visit.State);
        Assert.Equal(CaravanVisitStatus.AlreadyCancelled,
            visits.TryTrade(visit, MarketTradeSide.Purchase, ItemIds.Food, 1).Status);
        Assert.Equal(beforeRejected, State());

        // Health is deliberately outside the existing durable snapshot contract.
        var restoredCreature = world.Entities.Single(entity => entity.Id == creature.Id);
        Assert.Equal(restoredCreature.Health.Parts.Single(part => part.Name == "Torso").MaximumHealth,
            restoredCreature.Health.Parts.Single(part => part.Name == "Torso").CurrentHealth);
        return $"{seed}:{hit}:{trade}:{trip}:{State()}";

        (int, int, int, int) Totals()
        {
            var market = world.Stockpiles.Single().Contents;
            return (Count(market, ItemIds.Food), Count(market, ItemIds.Coin),
                Count(cargo, ItemIds.Food), Count(cargo, ItemIds.Coin));
        }

        string State() => WorldSnapshotSerializer.Serialize(world, clock.SimulationTime) + ":" +
            string.Join(",", cargo.Counts.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => $"{pair.Key}={pair.Value}"));

        void AssertCoreState()
        {
            Assert.Equal(new[] { "settlers", "visitors" }, world.FactionIds);
            Assert.Equal("settlers", world.Stockpiles.Single().FactionOwnerId);
            Assert.Equal("settlers", world.Entities.Single(entity => entity.Id == human.Id).AllegianceFactionId);
            Assert.Null(world.Entities.Single(entity => entity.Id == creature.Id).AllegianceFactionId);
            Assert.Equal(CreatureCaptivityState.Captive,
                world.Entities.Single(entity => entity.Id == creature.Id).CaptivityState);
            Assert.Equal(new[] { new FactionReputationState(human.Id, "visitors", 35) }, world.FactionReputations);
            Assert.Equal(new[] { new FactionHostilityState("settlers", "visitors", true),
                new FactionHostilityState("visitors", "settlers", false) }, world.FactionHostilities);
            var relation = Assert.Single(world.Relationships);
            Assert.Equal((creature.Id, human.Id), (relation.ObserverEntityId, relation.TargetEntityId));
            Assert.Equal((6d, 29d, 1d, 24d), (relation.Familiarity, relation.Trust, relation.Fear, relation.Loyalty));
        }
    }

    private static string Data(string file) => Path.Combine(AppContext.BaseDirectory, "data", file);
    private static int Count(ResourceContainer container, string item) => container.Counts.GetValueOrDefault(item);
    private static void Add(ResourceContainer container, string item, int quantity) =>
        Assert.Equal(ResourceMutationResult.Success, container.TryAdd(item, quantity));
}
