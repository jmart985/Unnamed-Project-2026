using Godot;
using Simulation;

namespace UnnamedProject2026;

/// <summary>Godot resources map stable item IDs to lazy-loaded presentations.</summary>
internal sealed class PlayerAppearanceAssets
{
    private readonly IReadOnlyDictionary<string, EquipableItem> _entries;
    public static PlayerAppearanceAssets Default { get; } = new();

    private PlayerAppearanceAssets()
    {
        var entries = new Dictionary<string, EquipableItem>(StringComparer.Ordinal);
        foreach (var definition in PlayerAppearanceCatalog.Default.Entries)
        {
            var item = GD.Load<EquipableItem>($"res://data/outfits/{definition.Id}.tres")
                ?? throw new InvalidDataException($"Missing presentation resource '{definition.Id}'.");
            item.Validate(definition);
            entries.Add(definition.Id, item);
        }
        _entries = entries;
    }

    public static string BodyPath(PlayerSex sex) => sex == PlayerSex.Male
        ? "res://assets/humanoids/player/player_male_base_rig_v1.glb"
        : "res://assets/humanoids/player/player_female_base_rig_v1.glb";
    public EquipableItem GetItem(string id) => _entries.TryGetValue(id, out var item) ? item
        : throw new InvalidDataException($"Unknown presentation resource '{id}'.");
    public string AssetPath(string id, PlayerSex sex) => GetItem(id).ModelPath(sex);
    public string? DrapeKind(string id) => GetItem(id).Drape switch
    {
        GarmentDrape.Skirt => "skirt", GarmentDrape.Cape => "cape", _ => null
    };
}

/// <summary>Owns presentation layers on one skeleton; equipment remains owned by World.</summary>
internal sealed class PlayerAppearanceLayers(Node3D model, Skeleton3D skeleton, PlayerCharacterProfile profile)
{
    private sealed record Layer(string Id, Node3D Root);
    private readonly Dictionary<string, Layer> _layers = new(StringComparer.Ordinal);
    private readonly PlayerBodyMask _bodyMask = new(model);
    private ClothingSpringModifier? _spring;
    public int CoveredBodyPixels => _bodyMask.CoveredPixels;
    public ClothingSpringModifier? Spring => _spring;

    public bool IsVisible(string id) => _layers.Values.Any(layer => layer.Id == id && layer.Root.Visible);
    public bool GenitaliaVisible => model.FindChildren("Anatomy_*", "MeshInstance3D", true, false)
        .OfType<MeshInstance3D>().Any(mesh => mesh.Visible);

    public void Apply(IReadOnlyDictionary<string, string> equipment, IEnumerable<string> hiddenSlots)
    {
        var appearance = profile.Appearance ?? new PlayerAppearance();
        var hidden = hiddenSlots.ToHashSet(StringComparer.Ordinal);
        var wanted = equipment.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        if (appearance.Hair != "none") wanted["hair"] = appearance.Hair;
        foreach (var slot in _layers.Keys.ToArray())
            if (!wanted.TryGetValue(slot, out var id) || id != _layers[slot].Id)
            {
                var root = _layers[slot].Root;
                root.Visible = false;
                root.GetParent().RemoveChild(root);
                root.QueueFree();
                _layers.Remove(slot);
            }
        foreach (var (slot, id) in wanted)
        {
            if (!PlayerAppearanceCatalog.Default.Supports(id, slot, profile.Sex)) continue;
            if (!_layers.ContainsKey(slot)) _layers.Add(slot, new(id, Attach(id)));
            _layers[slot].Root.Visible = !hidden.Contains(slot);
        }
        var covered = wanted.Any(pair => !hidden.Contains(pair.Key)
            && PlayerAppearanceCatalog.Default.TryGet(pair.Value, out var definition)
            && definition is { CoversGenitalia: true });
        foreach (var mesh in model.FindChildren("Anatomy_*", "MeshInstance3D", true, false).OfType<MeshInstance3D>())
            mesh.Visible = appearance.ShowGenitalia && !covered;
        var visible = _layers.Values.Where(layer => layer.Root.Visible).Select(layer => layer.Id).ToArray();
        _bodyMask.Apply(visible, profile.Sex);
        var kinds = visible.Select(PlayerAppearanceAssets.Default.DrapeKind).OfType<string>().Distinct().ToArray();
        if (_spring is null && kinds.Length > 0)
        {
            _spring = new ClothingSpringModifier { Name = "ClothingSprings" };
            skeleton.AddChild(_spring);
        }
        _spring?.SetKinds(kinds);
    }

    private Node3D Attach(string id)
    {
        var item = PlayerAppearanceAssets.Default.GetItem(id);
        var path = item.ModelPath(profile.Sex);
        var source = GD.Load<PackedScene>(path)?.Instantiate<Node3D>()
            ?? throw new InvalidDataException($"Cannot load appearance asset '{id}'.");
        model.AddChild(source);
        var sourceSkeleton = source.FindChildren("*", "Skeleton3D", true, false).OfType<Skeleton3D>().SingleOrDefault()
            ?? throw new InvalidDataException($"Appearance '{id}' has no skeleton.");
        var meshes = source.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>()
            .Where(mesh => item.MeshNodeNames.Count == 0 || item.MeshNodeNames.Contains(mesh.Name.ToString())).ToArray();
        if (meshes.Length == 0) throw new InvalidDataException($"Appearance '{id}' has no mesh.");
        var group = new Node3D { Name = "Appearance_" + id };
        skeleton.AddChild(group);
        foreach (var mesh in meshes)
        {
            var transform = skeleton.GlobalTransform.AffineInverse() * mesh.GlobalTransform;
            if (mesh.Skin is { } sourceSkin)
            {
                var skin = (Skin)sourceSkin.Duplicate();
                for (var index = 0; index < skin.GetBindCount(); index++)
                {
                    var name = skin.GetBindName(index).ToString();
                    if (string.IsNullOrEmpty(name)) name = sourceSkeleton.GetBoneName(skin.GetBindBone(index));
                    var target = skeleton.FindBone(name);
                    if (target < 0) throw new InvalidDataException($"Appearance '{id}' requires missing bone '{name}'.");
                    skin.SetBindBone(index, target);
                    skin.SetBindName(index, name);
                }
                mesh.Skin = skin;
            }
            mesh.Reparent(group, false);
            mesh.Transform = transform;
            mesh.Skeleton = mesh.GetPathTo(skeleton);
        }
        model.RemoveChild(source);
        source.QueueFree();
        return group;
    }
}
