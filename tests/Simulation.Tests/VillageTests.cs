using System.Numerics;
using System.Text.Json.Nodes;
using Simulation;

namespace Simulation.Tests;

public sealed class VillageTests
{
    private static SpeciesCatalog Catalog() => SpeciesCatalog.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "data", "species.json"));
    private static World Village() => NewGameSession.Create(Catalog(), PlayerCharacterProfile.Create("Villager"), createVillage: true).World;

    [Fact]
    public void TenDistinctHomesAreSpacedOnARingWithClearInwardEntrances()
    {
        var world = Village();
        Assert.True(world.UsesVillageLayout);
        Assert.Equal(10, world.Structures.Count);
        Assert.Equal(10, world.Structures.Select(home => home.ResidentId).Distinct().Count());
        foreach (var home in world.Structures)
        {
            Assert.InRange(home.Position.Length(), StarterVillage.Radius - 1, StarterVillage.Radius + 1);
            Assert.True(Vector3.Dot(home.DoorPosition - home.Position, -home.Position) > 0);
            Assert.Single(home.Beds);
            Assert.False(home.IsClosed);
            Assert.True(world.TryGetHomePosition(home.ResidentId!, out var destination));
            Assert.Equal(home.Position, destination);
            Assert.True(Math.Abs(home.Position.X) > HouseGeometry.Width / 2 + 2);
            Assert.True(Math.Abs(home.Position.Z) > HouseGeometry.Depth / 2 + 2);
            foreach (var other in world.Structures.Where(other => other != home))
                Assert.True(Math.Abs(other.Position.X - home.Position.X) > HouseGeometry.Width + 2
                    || Math.Abs(other.Position.Z - home.Position.Z) > HouseGeometry.Depth + 2);
        }
    }

    [Fact]
    public void TwoMinuteDayPreservesHoursAndReportsActualSecondsToTransition()
    {
        var world = Village();
        var npc = world.Structures[0].ResidentId!;
        world.SetWorkSchedule(npc, 8, 18);
        Assert.Equal(120, world.DayLengthSeconds);
        Assert.True(world.EvaluateWorkSchedule(npc, 49.9).IsWorking);
        Assert.False(world.EvaluateWorkSchedule(npc, 50).IsWorking);
        Assert.Equal(70, world.EvaluateWorkSchedule(npc, 50).SecondsUntilTransition);
        Assert.Equal(18, world.DayHour(50));
        Assert.True(world.EvaluateWorkSchedule(npc, 120).IsWorking);
    }

    [Fact]
    public void HomesOrientationDoorStateAndDayLengthRoundTrip()
    {
        var world = Village();
        world.Structures[0].IsClosed = true;
        var json = WorldSnapshotSerializer.Serialize(world, 57);
        var loaded = new World();
        WorldSnapshotSerializer.Load(loaded, json, Catalog());
        Assert.Equal(json, WorldSnapshotSerializer.Serialize(loaded, 57));
        Assert.Equal(120, loaded.DayLengthSeconds);
        Assert.True(loaded.UsesVillageLayout);
        Assert.All(loaded.Structures, home => Assert.True(loaded.TryGetHomePosition(home.ResidentId!, out var point) && point == home.Position));
    }

    [Fact]
    public void PreviousSaveKeepsItsClockAndDoesNotRelocateCustomHomes()
    {
        var world = NewGameSession.Create(Catalog(), PlayerCharacterProfile.Create("Legacy")).World;
        var json = JsonNode.Parse(WorldSnapshotSerializer.Serialize(world, 75))!;
        json["SchemaVersion"] = 27;
        json.AsObject().Remove("DayLengthSeconds");
        json.AsObject().Remove("UsesVillageLayout");
        var loaded = new World();
        WorldSnapshotSerializer.Load(loaded, json.ToJsonString(), Catalog());
        Assert.Equal(240, loaded.DayLengthSeconds);
        Assert.Equal(world.HomePositions, loaded.HomePositions);
        Assert.Empty(loaded.Structures);
    }

    [Theory]
    [InlineData("missing-clock")] [InlineData("zero-clock")] [InlineData("rotation")]
    [InlineData("resident")] [InlineData("duplicate-resident")]
    public void InvalidHousingSnapshotLeavesLiveWorldUntouched(string defect)
    {
        var world = Village();
        var before = WorldSnapshotSerializer.Serialize(world, 5);
        var json = JsonNode.Parse(before)!;
        switch (defect)
        {
            case "missing-clock": json.AsObject().Remove("DayLengthSeconds"); break;
            case "zero-clock": json["DayLengthSeconds"] = 0; break;
            case "rotation": json["Structures"]![0]!["FacingQuarterTurns"] = 4; break;
            case "resident": json["Structures"]![0]!["ResidentId"] = "missing"; break;
            case "duplicate-resident": json["Structures"]![1]!["ResidentId"] = json["Structures"]![0]!["ResidentId"]!.GetValue<string>(); break;
        }
        Assert.Throws<InvalidDataException>(() => WorldSnapshotSerializer.Load(world, json.ToJsonString(), Catalog()));
        Assert.Equal(before, WorldSnapshotSerializer.Serialize(world, 5));
    }

    [Fact]
    public void WiderHouseRejectsCornerOverlapAndRetainsFlushThreshold()
    {
        var world = Village();
        var house = world.Structures[0];
        var position = house.Position + new Vector3(7, 0, 7);
        world.TrySetEntityPosition(world.PlayerEntityId!, position);
        var result = new ActorConstructionService(world, Catalog()).ValidateHutPlacement(world.PlayerEntityId!, position);
        Assert.Equal(ConstructionActionResult.Overlap, result.Result);
        var floor = HouseGeometry.SolidParts(false).Single(part => part.Name == "Floor");
        Assert.Equal(0, floor.Center.Y + floor.Size.Y / 2);
        Assert.Equal(8, SettlementConstruction.HutWidth);
        Assert.Equal(8, SettlementConstruction.HutDepth);
        Assert.Equal(2.4f, HouseGeometry.DoorWidth);
        Assert.Equal(2.6f, HouseGeometry.DoorHeight);
    }

    [Fact]
    public void ResidentWalksToClosedDoorBeforeOpeningItAndContinuesHome()
    {
        var world = Village();
        var house = world.Structures[0];
        var resident = world.Entities.Single(entity => entity.Id == house.ResidentId);
        var nav = new FakeNavigationAdapter();
        var service = new WoodHaulingService(world, _ => nav);
        house.IsClosed = true;
        world.TrySetEntityPosition(resident.Id, house.ToWorld(new(0, 0, 9)));
        Assert.True(service.ReconcileSchedule(resident, 60));
        Assert.True(house.IsClosed);
        var approach = nav.RequestTargets.Single();
        Assert.Equal(house.ToWorld(new(0, 0, HouseGeometry.DoorZ + 1.25f)), approach.Value);
        world.TrySetEntityPosition(resident.Id, approach.Value);
        service.ReconcileSchedule(resident, 60);
        Assert.False(house.IsClosed);
        Assert.Equal(NavigationOutcome.Cancelled, nav.Requests[approach.Key]);
        Assert.Equal(house.Position, nav.RequestTargets.Last().Value);
    }

    [Fact]
    public void FullStorageAllowsRestWithCargoInsteadOfEndlessOffShiftDelivery()
    {
        var world = Village();
        var resident = world.Entities.First(entity => entity.Id == world.Structures[0].ResidentId);
        var stockpile = world.Stockpiles.Single();
        stockpile.Contents.TryAdd(ItemIds.Wood, stockpile.Contents.Capacity);
        resident.TryAddCarried(ItemIds.Wood, 5);
        var nav = new FakeNavigationAdapter();
        var service = new WoodHaulingService(world, _ => nav);
        Assert.True(service.ReconcileSchedule(resident, 60));
        Assert.Equal("Going home", resident.Activity);
        Assert.Equal(5, resident.Inventory[ItemIds.Wood]);
        Assert.Equal(world.Structures[0].Position, nav.RequestTargets.Single().Value);
        Assert.False(service.ReconcileSchedule(resident, 120));
        Assert.Equal(5, resident.Inventory[ItemIds.Wood]);
    }

    [Fact]
    public void RemovingResidentLeavesAUsableVacantHouseAndSavableWorld()
    {
        var world = Village();
        var house = world.Structures[0];
        Assert.True(world.TryRemoveEntity(house.ResidentId!));
        Assert.Null(house.ResidentId);
        var loaded = new World();
        WorldSnapshotSerializer.Load(loaded, WorldSnapshotSerializer.Serialize(world, 0), Catalog());
        Assert.Null(loaded.Structures.Single(item => item.Id == house.Id).ResidentId);
        Assert.Equal(10, loaded.Structures.Count);
    }

    [Fact]
    public void ClosingDoorDuringWorkReleasesBlockedHaulAndResumesAfterOpening()
    {
        var world = Village();
        var house = world.Structures[0];
        var resident = world.Entities.Single(entity => entity.Id == house.ResidentId);
        var nav = new FakeNavigationAdapter();
        var service = new WoodHaulingService(world, _ => nav);
        world.TrySetEntityPosition(resident.Id, house.Position);
        var employment = world.GetEmployment(resident.Id);
        Assert.Equal(WoodHaulingStatus.TravelingToSource, service.Advance(resident, Catalog().GetRequired("human"), 1, 0).Status);
        var pickup = nav.Requests.Single().Key;
        house.IsClosed = true;
        Assert.True(service.ReconcileSchedule(resident, 0));
        Assert.Equal(NavigationOutcome.Cancelled, nav.Requests[pickup]);
        Assert.Equal(ActionKind.Wander, resident.Actions.Current!.Kind);
        var doorstep = nav.RequestTargets.Last().Value;
        Assert.Equal(house.ToWorld(new(0, 0, HouseGeometry.DoorZ - 1.25f)), doorstep);
        world.TrySetEntityPosition(resident.Id, doorstep);
        Assert.False(service.ReconcileSchedule(resident, 0));
        Assert.False(house.IsClosed);
        Assert.Equal(WoodHaulingStatus.TravelingToSource, service.Advance(resident, Catalog().GetRequired("human"), 2, 0).Status);
        Assert.Equal(employment, world.GetEmployment(resident.Id));
        Assert.Equal(100, world.ResourcePiles.Single().Contents.Counts[ItemIds.Wood]);
        Assert.Empty(resident.Inventory);
    }
}
