using System.Numerics;
using System.Text;
using System.Text.Json.Nodes;
using Simulation;

namespace Simulation.Tests;

public sealed class SpeciesCapabilityTests
{
    [Fact]
    public void OmittedOrDisabledRoleCapabilitiesRejectUnsupportedOperations()
    {
        var catalog = LoadCatalog(_ => { });
        var world = new World { SpeciesCatalog = catalog };
        var ape = world.SpawnEntity(catalog.GetRequired("ape"), Vector3.Zero);

        Assert.Throws<ArgumentException>(() => world.SetSessionPlayer(
            42, ape.Id, PlayerCharacterProfile.Create("Ape Player")));
        Assert.Equal(SquadResult.IneligibleEntity, world.AddSquadMember(ape.Id));
        Assert.False(world.HasDailySchedule(ape));
        Assert.Equal(ScheduleUpdateResult.IneligibleEntity, world.SetWorkSchedule(ape.Id, 8, 16));
    }

    [Fact]
    public void WorldEligibilityAndSocialActionsFollowSpeciesCapabilities()
    {
        var catalog = LoadCatalog(ape =>
        {
            ape["canBePlayerCharacter"] = true;
            ape["canJoinPlayerSquad"] = true;
            ape["canHaveWorkSchedule"] = true;
            ape["canBeCaptured"] = false;
            ape["canBePetted"] = false;
        });
        var world = new World { SpeciesCatalog = catalog };
        var profile = PlayerCharacterProfile.Create("Ape Player");
        var player = world.SpawnEntity(catalog.GetRequired("ape"), Vector3.Zero, sex: BiologicalSex.Female);

        world.SetSessionPlayer(42, player.Id, profile);

        var worker = world.SpawnEntity(catalog.GetRequired("ape"), Vector3.One);
        Assert.True(world.HasDailySchedule(worker));
        Assert.Equal(SquadResult.Accepted, world.AddSquadMember(worker.Id));
        Assert.Equal(ScheduleUpdateResult.Accepted, world.SetWorkSchedule(worker.Id, 8, 16));
        Assert.Equal(CreatureCaptureResult.NotCreature, world.TryCaptureCreature(worker.Id));

        var events = RelationshipEventCatalog.LoadFromFile(Path.Combine(
            AppContext.BaseDirectory, "data", "relationship_events.json"));
        var goods = TradeGoodsCatalog.LoadFromFile(Path.Combine(
            AppContext.BaseDirectory, "data", "trade_goods.json"));
        var resolver = new InteractionResolver(world, goods, new RelationshipEventService(world, events));
        var petResult = resolver.Resolve(new InteractionRequest(
            player.Id, worker.Id, InteractionType.Physical, "pet"));

        Assert.False(petResult.Succeeded);
        Assert.Contains("cannot be petted", petResult.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SnapshotValidationUsesCapabilityDataWithoutChangingSchema()
    {
        var enabledCatalog = LoadCatalog(ape =>
        {
            ape["canBePlayerCharacter"] = true;
            ape["canJoinPlayerSquad"] = true;
            ape["canHaveWorkSchedule"] = true;
        });
        var source = new World { SpeciesCatalog = enabledCatalog };
        var player = source.SpawnEntity(enabledCatalog.GetRequired("ape"), Vector3.Zero, sex: BiologicalSex.Female);
        source.SetSessionPlayer(42, player.Id, PlayerCharacterProfile.Create("Ape Player"));
        var worker = source.SpawnEntity(enabledCatalog.GetRequired("ape"), Vector3.One);
        Assert.Equal(SquadResult.Accepted, source.AddSquadMember(worker.Id));
        Assert.Equal(ScheduleUpdateResult.Accepted, source.SetWorkSchedule(worker.Id, 8, 16));
        Assert.Equal(ScheduleUpdateResult.Accepted, source.SetHomePosition(worker.Id, new Vector3(2, 0, 0)));
        var json = WorldSnapshotSerializer.Serialize(source, 1);

        var restored = new World();
        WorldSnapshotSerializer.Load(restored, json, enabledCatalog);

        Assert.Equal(WorldSnapshotSerializer.CurrentSchemaVersion,
            WorldSnapshotSerializer.Deserialize(json).SchemaVersion);
        Assert.Equal(json, WorldSnapshotSerializer.Serialize(restored, 1));
        Assert.Contains(worker.Id, restored.SquadMemberIds);
        Assert.Equal(new NpcWorkSchedule(8, 16), restored.GetWorkSchedule(worker.Id));
        Assert.True(restored.TryGetHomePosition(worker.Id, out var home));
        Assert.Equal(new Vector3(2, 0, 0), home);

        var incompatibleCatalog = LoadCatalog(ape => ape["canBePlayerCharacter"] = true);
        var target = new World();
        var existing = target.SpawnEntity("human", Vector3.One);
        Assert.Throws<InvalidDataException>(() => WorldSnapshotSerializer.Load(target, json, incompatibleCatalog));
        Assert.Same(existing, Assert.Single(target.Entities));
    }

    private static SpeciesCatalog LoadCatalog(Action<JsonObject> configureApe)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "data", "species.json");
        var root = JsonNode.Parse(File.ReadAllText(path))!.AsArray();
        var ape = root.OfType<JsonObject>().Single(entry =>
            string.Equals(entry["id"]?.GetValue<string>(), "ape", StringComparison.Ordinal));
        configureApe(ape);

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(root.ToJsonString()));
        return SpeciesCatalog.Load(stream);
    }
}
