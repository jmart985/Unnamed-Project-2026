using Godot;
using Simulation;

namespace UnnamedProject2026;

public partial class Main
{
    private string _houseDoorState = string.Empty;
    private string _houseNavigationState = string.Empty;
    private ulong _houseNavigationChangedFrame = ulong.MaxValue;

    private void BuildVillagePaths()
    {
        if (!_world.UsesVillageLayout) return;
        var pathColor = new Color(.53f, .44f, .31f);
        void Path(string name, Vector3 from, Vector3 to, float width)
        {
            var direction = to - from;
            AddScenery(name, new Vector3(width, .03f, direction.Length() + .15f),
                (from + to) / 2 + new Vector3(0, .015f, 0), pathColor);
            _scenery!.GetNode<MeshInstance3D>(name).Rotation = new Vector3(0, Mathf.Atan2(direction.X, direction.Z), 0);
        }
        const int segments = 64;
        const float pathRadius = 37;
        for (var index = 0; index < segments; index++)
        {
            var a = index * Mathf.Tau / segments;
            var b = (index + 1) * Mathf.Tau / segments;
            Path($"VillageLane{index}", new(Mathf.Cos(a) * pathRadius, 0, Mathf.Sin(a) * pathRadius),
                new(Mathf.Cos(b) * pathRadius, 0, Mathf.Sin(b) * pathRadius), 2.8f);
        }
        foreach (var house in _world.Structures.Where(house => house.ResidentId is not null))
        {
            var porch = house.ToWorld(new System.Numerics.Vector3(0, 0, 4.1f));
            var nearest = new Vector2(porch.X, porch.Z).Normalized() * pathRadius;
            Path($"Path-{house.Id}", new(porch.X, 0, porch.Z), new(nearest.X, 0, nearest.Y), 2.4f);
        }
    }

    private void SyncHouseDoorNavigation()
    {
        var state = string.Join('|', _world.Structures.Select(house => $"{house.Id}:{house.IsClosed}:{house.IsLocked}"));
        if (_houseDoorState == state) return;
        _houseDoorState = state;
        SyncConstructionPresentation();
        var navigationState = string.Join('|', _world.Structures.Select(house => $"{house.Id}:{house.IsClosed}"));
        if (_houseNavigationState == navigationState) return;
        _houseNavigationState = navigationState;
        _navigationRegion.NavigationMesh = CreateNavigationMesh(CurrentWorkbenchPositions(), _world.Structures);
        // NavigationServer merges region changes at the physics boundary. Querying
        // the just-opened doorway in this frame can incorrectly fail an order forever.
        _houseNavigationChangedFrame = Engine.GetPhysicsFrames();
    }
}
