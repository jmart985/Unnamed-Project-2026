using Godot;
using Simulation;

namespace UnnamedProject2026;

public partial class Main
{
    private enum TargetKind { Entity, Food, ResourcePile, Stockpile, Workstation, Farm }
    private sealed record WorldTarget(TargetKind Kind, string Id);
    private sealed record TargetView(WorldTarget Target, Node3D Node, Aabb Bounds, Aabb[] Parts, Vector3[] Samples)
    {
        public Vector3 Center => Bounds.GetCenter();
    }

    private const float TargetRange = 40;
    private Label _targetHud = null!;
    private MeshInstance3D _targetRing = null!;

    private void ConfigureWorldSelection()
    {
        _targetHud = new Label
        {
            Name = "WorldTarget", AnchorTop = 1, AnchorBottom = 1,
            OffsetLeft = 20, OffsetTop = -142, OffsetRight = 390, OffsetBottom = -16,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _targetHud.AddThemeColorOverride("font_color", new Color(1, 0.95f, 0.6f));
        _targetHud.AddThemeColorOverride("font_shadow_color", Colors.Black);
        _targetHud.AddThemeConstantOverride("shadow_offset_x", 2);
        _targetHud.AddThemeConstantOverride("shadow_offset_y", 2);
        _menuHud.AddChild(_targetHud);
        _targetRing = new MeshInstance3D
        {
            Name = "SelectionRing", Mesh = new TorusMesh { InnerRadius = 0.65f, OuterRadius = 0.75f },
            Scale = new Vector3(1, 0.12f, 1), Visible = false,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(1, 0.85f, 0.1f),
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
            }
        };
        AddChild(_targetRing);
    }

    private bool TargetExists(WorldTarget target) => target.Kind switch
    {
        TargetKind.Entity => target.Id != _world.PlayerEntityId && _world.TryGetEntity(target.Id, out _),
        TargetKind.Food => _world.TryGetFoodSource(target.Id, out _),
        TargetKind.ResourcePile => _world.ResourcePiles.Any(pile => pile.Id == target.Id),
        TargetKind.Stockpile => _world.Stockpiles.Any(pile => pile.Id == target.Id),
        TargetKind.Workstation => _world.Workstations.Any(item => item.Id == target.Id),
        TargetKind.Farm => _world.Farms.Any(item => item.Id == target.Id),
        _ => false
    };

    private List<TargetView> WorldTargets()
    {
        var targets = new List<TargetView>();
        foreach (var (id, node) in _entityViews)
        {
            var target = new WorldTarget(TargetKind.Entity, id);
            if (!TargetExists(target) || !node.IsVisibleInTree() || node.IsQueuedForDeletion()) continue;
            var meshes = node.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>()
                .Where(mesh => mesh.IsVisibleInTree()).ToArray();
            if (meshes.Length == 0) continue;
            var bounds = meshes[0].GlobalTransform * meshes[0].GetAabb();
            foreach (var mesh in meshes.Skip(1)) bounds = bounds.Merge(mesh.GlobalTransform * mesh.GetAabb());
            var parts = meshes.Select(mesh => mesh.GlobalTransform * mesh.GetAabb()).ToArray();
            var samples = meshes.SelectMany(mesh => SampleMesh(mesh)).ToArray();
            targets.Add(new(target, node, bounds, parts, samples));
        }
        foreach (var (target, marker) in _resourceMarkers)
            if (TargetExists(target) && marker.IsVisibleInTree() && !marker.IsQueuedForDeletion())
                targets.Add(new(target, marker, marker.GlobalTransform * marker.GetAabb(),
                    [marker.GlobalTransform * marker.GetAabb()], SampleMesh(marker).ToArray()));
        foreach (var workstation in _world.Workstations)
        {
            var target = new WorldTarget(TargetKind.Workstation, workstation.Id);
            if (!TargetExists(target) || !_workbench.IsVisibleInTree() || _workbench.IsQueuedForDeletion()) continue;
            var meshes = _workbench.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>()
                .Append(_workbench).Where(mesh => mesh.Mesh is not null && mesh.IsVisibleInTree()).ToArray();
            if (meshes.Length == 0) continue;
            var parts = meshes.Select(mesh => mesh.GlobalTransform * mesh.GetAabb()).ToArray();
            var bounds = parts.Aggregate((left, right) => left.Merge(right));
            targets.Add(new(target, _workbench, bounds, parts, meshes.SelectMany(SampleMesh).ToArray()));
        }
        foreach (var farm in _world.Farms)
        {
            var target = new WorldTarget(TargetKind.Farm, farm.Id);
            if (!TargetExists(target) || !_farmView.IsVisibleInTree() || _farmView.IsQueuedForDeletion()) continue;
            var meshes = _farmView.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>()
                .Where(mesh => mesh.Mesh is not null && mesh.IsVisibleInTree()).ToArray();
            if (meshes.Length == 0) continue;
            var parts = meshes.Select(mesh => mesh.GlobalTransform * mesh.GetAabb()).ToArray();
            targets.Add(new(target, _farmView, parts.Aggregate((left, right) => left.Merge(right)), parts,
                meshes.SelectMany(SampleMesh).ToArray()));
        }
        return targets;
    }

    private bool TargetVisible(TargetView target, Camera3D camera, IReadOnlyList<TargetView> targets)
    {
        if (target.Node.GlobalPosition.DistanceTo(_playerCharacter.GlobalPosition) > TargetRange) return false;
        return target.Samples.Any(point => camera.IsPositionInFrustum(point)
            && GetViewport().GetVisibleRect().HasPoint(camera.UnprojectPosition(point))
            && TargetLineClear(camera.GlobalPosition, point, target.Target, targets));
    }

    private static IEnumerable<Vector3> SampleMesh(MeshInstance3D mesh)
    {
        var bounds = mesh.GetAabb();
        foreach (var height in new[] { 0.25f, 0.5f, 0.85f })
            yield return mesh.GlobalTransform * new Vector3(bounds.GetCenter().X, bounds.Position.Y + bounds.Size.Y * height, bounds.GetCenter().Z);
    }

    private bool TargetLineClear(Vector3 from, Vector3 point, WorldTarget target, IReadOnlyList<TargetView> targets)
    {
        // Existing scenery is visual-only, so inspect its actual mesh bounds instead of adding physics bodies.
        var scenery = _scenery?.GetChildren().OfType<MeshInstance3D>() ?? [];
        var benchParts = target.Kind == TargetKind.Workstation
            ? Enumerable.Empty<MeshInstance3D>()
            : _workbench.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>().Append(_workbench);
        var farmParts = target.Kind == TargetKind.Farm
            ? Enumerable.Empty<MeshInstance3D>()
            : _farmView.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>();
        foreach (var mesh in scenery.Append(_map).Concat(benchParts).Concat(farmParts))
        {
            if (mesh.Mesh is null || !mesh.IsVisibleInTree()) continue;
            var inverse = mesh.GlobalTransform.AffineInverse();
            if (mesh.GetAabb().IntersectsSegment(inverse * from, inverse * point)) return false;
        }
        // Check actual mesh parts, not the empty gaps in an animated character's aggregate bounds.
        return !targets.Any(other => other.Target != target && other.Parts.Any(part => part.IntersectsSegment(from, point)));
    }

    private List<TargetView> VisibleWorldTargets()
    {
        var camera = GetViewport().GetCamera3D();
        if (camera is null) return [];
        var targets = WorldTargets();
        // Stable identity order avoids changing the cycle when characters animate or move.
        return targets.Where(target => TargetVisible(target, camera, targets))
            .OrderBy(target => target.Target.Kind).ThenBy(target => target.Target.Id, StringComparer.Ordinal).ToList();
    }

    private void CycleWorldTarget()
    {
        var targets = VisibleWorldTargets();
        var previous = targets.FindIndex(target => target.Target == _selectedTarget);
        var camera = GetViewport().GetCamera3D();
        var center = GetViewport().GetVisibleRect().GetCenter();
        _selectedTarget = targets.Count == 0 ? null : previous < 0
            ? targets.OrderBy(target => camera.UnprojectPosition(target.Center).DistanceSquaredTo(center))
                .ThenBy(target => target.Center.DistanceSquaredTo(camera.GlobalPosition)).First().Target
            : targets[(previous + 1) % targets.Count].Target;
        if (_selectedEntityId is { } id) ShowSelectedSchedule(id);
        _lastActionMessage = _selectedTarget is { } target ? $"Selected {target.Kind}: {target.Id}." : "No visible targets within 40 m.";
        UpdateInspector();
        UpdateWorldSelection();
    }

    private void UpdateWorldSelection()
    {
        if (_targetHud is null) return;
        if (_selectedTarget is { } selected && !TargetExists(selected)) _selectedTarget = null;
        var gameplay = _sessionActive && _menuState.ActiveMenu is null;
        _targetHud.Visible = gameplay;
        UpdateGameplayFeedback(0);
        _targetRing.Visible = false;
        ((StandardMaterial3D)_targetRing.MaterialOverride).AlbedoColor = new Color(1, 0.85f, 0.1f);
        var binding = PlayerButtonBindings.Label(_buttonBindings.Get(ControllerActions.CycleTarget).Desktop);
        var hint = $"{binding}: select / cycle visible targets (40 m)";
        if (_selectedTarget is not { } target)
        {
            _targetHud.Text = $"TARGET: None\n{hint}";
            return;
        }
        var targets = WorldTargets();
        var view = targets.FirstOrDefault(view => view.Target == target);
        if (view is null) { _selectedTarget = null; _targetHud.Text = $"TARGET: None\n{hint}"; return; }
        var camera = GetViewport().GetCamera3D();
        var visible = camera is not null && TargetVisible(view, camera, targets);
        _targetRing.Visible = gameplay && visible;
        _targetRing.GlobalPosition = new Vector3(view.Node.GlobalPosition.X, view.Node.GlobalPosition.Y + 0.1f, view.Node.GlobalPosition.Z);
        string detail;
        if (target.Kind == TargetKind.Entity && _world.TryGetEntity(target.Id, out var entity) && entity is not null)
        {
            detail = $"{entity.SpeciesId} · HP {entity.Health.Parts.Sum(part => part.CurrentHealth):0}/{entity.Health.Parts.Sum(part => part.MaximumHealth):0} · Satiety {entity.Satiety:0}\n{(entity.Health.IsDead ? "Dead" : entity.Health.IsIncapacitated ? "Incapacitated" : "Conscious")} · {entity.Activity}";
            if (_world.PlayerEntityId is { } playerId)
            {
                var bond = _world.GetBondLevel(entity.Id, playerId);
                detail += $" · {bond}";
                if (entity.SpeciesId != "human")
                {
                    var trusted = bond is RelationshipBondLevel.Trusted or RelationshipBondLevel.Loyal;
                    var canPet = trusted && !entity.Health.IsIncapacitated;
                    detail += canPet ? " · Pet available" : " · Pet unavailable";
                    if (trusted) ((StandardMaterial3D)_targetRing.MaterialOverride).AlbedoColor = new Color(0.2f, 1, 0.45f);
                }
            }
        }
        else if (target.Kind == TargetKind.Workstation)
        {
            var workstation = _world.Workstations.Single(item => item.Id == target.Id);
            var recipe = workstation.CurrentRecipe;
            detail = recipe is null ? "Idle · no active recipe" :
                $"{recipe.Id} · work {recipe.WorkProgress}/{recipe.WorkRequired}\nInputs: {ContainerSummary(workstation.InputStorage)} · Outputs: {ContainerSummary(workstation.OutputStorage)}";
        }
        else if (target.Kind == TargetKind.Farm)
        {
            var plot = _world.Farms.Single(item => item.Id == target.Id).Plots.First();
            detail = !plot.IsPlanted ? "Empty plot · plant 1 carried plant" : plot.IsMature
                ? $"{plot.Crop!.Id} · mature · {plot.Crop.HarvestYield} food ready"
                : $"{plot.Crop!.Id} · growing {plot.Growth}/{plot.Crop.GrowthRequired}";
        }
        else
        {
            var contents = target.Kind switch
            {
                TargetKind.Food => _world.FoodSources.First(source => source.Id == target.Id).Contents,
                TargetKind.ResourcePile => _world.ResourcePiles.First(source => source.Id == target.Id).Contents,
                _ => _world.Stockpiles.First(source => source.Id == target.Id).Contents
            };
            detail = contents.Counts.Count == 0 ? "Empty" : string.Join(", ", contents.Counts.OrderBy(item => item.Key).Select(item => $"{item.Key}: {item.Value}"));
        }
        _targetHud.Text = $"TARGET: {target.Kind} · {target.Id}{(visible ? "" : " (out of view)")}\n{detail}\n{hint}";
    }

    private static string ContainerSummary(ResourceContainer container) => container.Counts.Count == 0
        ? "empty"
        : string.Join(", ", container.Counts.OrderBy(item => item.Key).Select(item => $"{item.Key}: {item.Value}"));
}
