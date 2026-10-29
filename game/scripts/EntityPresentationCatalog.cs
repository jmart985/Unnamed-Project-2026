using System.Text.Json;
using Godot;
using Simulation;

namespace UnnamedProject2026;

/// <summary>Godot-side model and fallback presentation data, separate from simulation species rules.</summary>
public sealed class EntityPresentationCatalog
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly Dictionary<string, EntityPresentationDefinition> _entries = new(StringComparer.OrdinalIgnoreCase);

    private EntityPresentationCatalog() { }

    public static EntityPresentationCatalog Empty { get; } = new();

    public IReadOnlyCollection<EntityPresentationDefinition> Definitions => _entries.Values.Distinct().ToArray();

    public static EntityPresentationCatalog LoadFromResource(string resourcePath)
    {
        if (string.IsNullOrWhiteSpace(resourcePath))
            throw new ArgumentException("A presentation catalog resource path is required.", nameof(resourcePath));

        using var file = Godot.FileAccess.Open(resourcePath, Godot.FileAccess.ModeFlags.Read);
        if (file is null)
            throw new InvalidDataException($"Presentation catalog '{resourcePath}' could not be opened.");

        return LoadFromJson(file.GetAsText());
    }

    public static EntityPresentationCatalog LoadFromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new InvalidDataException("Presentation catalog JSON is empty.");

        PresentationCatalogData? data;
        try
        {
            data = JsonSerializer.Deserialize<PresentationCatalogData>(json, JsonOptions);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Presentation catalog JSON is invalid.", exception);
        }

        if (data?.Species is null || data.Species.Count == 0)
            throw new InvalidDataException("Presentation catalog must define at least one species presentation.");

        var catalog = new EntityPresentationCatalog();
        foreach (var entry in data.Species)
        {
            if (entry is null)
                throw new InvalidDataException("Presentation catalog cannot contain a null species entry.");

            var id = RequiredText(entry.Id, "species id");
            var definition = EntityPresentationDefinition.Create(entry, id);
            catalog.Add(id, definition);

            foreach (var alias in entry.Aliases ?? [])
            {
                var aliasId = RequiredText(alias, $"alias for species '{id}'");
                catalog.Add(aliasId, definition);
            }
        }

        return catalog;
    }

    public EntityPresentationDefinition Resolve(string? speciesId, bool isCreature, string? bodyCategory)
    {
        if (!string.IsNullOrWhiteSpace(speciesId) && _entries.TryGetValue(speciesId, out var definition))
            return definition;

        return EntityPresentationDefinition.CreateFallback(
            CreatureBodyPlanResolver.Resolve(bodyCategory, isCreature), isCreature);
    }

    private void Add(string id, EntityPresentationDefinition definition)
    {
        if (!_entries.TryAdd(id, definition))
            throw new InvalidDataException($"Presentation species id or alias '{id}' is defined more than once.");
    }

    private static string RequiredText(string? value, string description)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidDataException($"Presentation catalog {description} cannot be empty.");
        return value.Trim();
    }

    internal sealed class PresentationCatalogData
    {
        public PresentationCatalogData() { }
        public List<PresentationEntryData?>? Species { get; init; }
    }

    internal sealed class PresentationEntryData
    {
        public PresentationEntryData() { }
        public string? Id { get; init; }
        public string[]? Aliases { get; init; }
        public string? ModelPath { get; init; }
        public string? MaleModelPath { get; init; }
        public string? FemaleModelPath { get; init; }
        public string? DisplayName { get; init; }
        public string? MaleDisplayName { get; init; }
        public float? ModelHeight { get; init; }
        public float? ModelCollisionRadius { get; init; }
        public float[]? BodyColor { get; init; }
        public float[]? AccentColor { get; init; }
        public float[]? LowerBodyColor { get; init; }
        public bool VaryPrimaryColorByEntity { get; init; }
        public float[]? ProceduralBodySize { get; init; }
        public float? FallbackCollisionHeight { get; init; }
        public float? FallbackCollisionRadius { get; init; }
        public string? LocomotionFamily { get; init; }
        public UprightPresentationData? Upright { get; init; }
    }

    internal sealed class UprightPresentationData
    {
        public UprightPresentationData() { }
        public float? LegLength { get; init; }
        public float? ShoulderWidth { get; init; }
        public float[]? ChestSize { get; init; }
        public float? ArmLength { get; init; }
        public float? ForwardLeanRadians { get; init; }
        public float? BobHeight { get; init; }
        public float? ArmThickness { get; init; }
        public float? LegThickness { get; init; }
    }

    public sealed class EntityPresentationDefinition
    {
        private EntityPresentationDefinition(
            string speciesId,
            string? modelPath,
            string? maleModelPath,
            string? femaleModelPath,
            string displayName,
            string? maleDisplayName,
            float modelHeight,
            float modelCollisionRadius,
            Color bodyColor,
            Color accentColor,
            Color lowerBodyColor,
            bool varyPrimaryColorByEntity,
            Vector3 proceduralBodySize,
            float fallbackCollisionHeight,
            float fallbackCollisionRadius,
            string locomotionFamily,
            UprightPresentation upright)
        {
            SpeciesId = speciesId;
            ModelPath = modelPath;
            MaleModelPath = maleModelPath;
            FemaleModelPath = femaleModelPath;
            DisplayName = displayName;
            MaleDisplayName = maleDisplayName;
            ModelHeight = modelHeight;
            ModelCollisionRadius = modelCollisionRadius;
            BodyColor = bodyColor;
            AccentColor = accentColor;
            LowerBodyColor = lowerBodyColor;
            VaryPrimaryColorByEntity = varyPrimaryColorByEntity;
            ProceduralBodySize = proceduralBodySize;
            FallbackCollisionHeight = fallbackCollisionHeight;
            FallbackCollisionRadius = fallbackCollisionRadius;
            LocomotionFamily = locomotionFamily;
            Upright = upright;
        }

        public string SpeciesId { get; }
        public string? ModelPath { get; }
        public string? MaleModelPath { get; }
        public string? FemaleModelPath { get; }
        public string DisplayName { get; }
        public string? MaleDisplayName { get; }
        public float ModelHeight { get; }
        public float ModelCollisionRadius { get; }
        public Color BodyColor { get; }
        public Color AccentColor { get; }
        public Color LowerBodyColor { get; }
        public bool VaryPrimaryColorByEntity { get; }
        public Vector3 ProceduralBodySize { get; }
        public float FallbackCollisionHeight { get; }
        public float FallbackCollisionRadius { get; }
        public string LocomotionFamily { get; }
        public UprightPresentation Upright { get; }

        public string? GetModelPath(BiologicalSex sex) => sex switch
        {
            BiologicalSex.Male => MaleModelPath ?? ModelPath ?? FemaleModelPath,
            BiologicalSex.Female => FemaleModelPath ?? ModelPath ?? MaleModelPath,
            _ => ModelPath ?? FemaleModelPath ?? MaleModelPath
        };

        public string? GetDefaultModelPath() => ModelPath ?? FemaleModelPath ?? MaleModelPath;

        public string GetDisplayName(BiologicalSex sex) =>
            sex == BiologicalSex.Male && !string.IsNullOrWhiteSpace(MaleDisplayName)
                ? MaleDisplayName!
                : DisplayName;

        public Color ResolveBodyColor(float variation) => VaryPrimaryColorByEntity
            ? Color.FromHsv(Mathf.Clamp(variation, 0, 1), 0.45f, 0.75f)
            : BodyColor;

        internal static EntityPresentationDefinition Create(PresentationEntryData data, string id)
        {
            var modelPath = OptionalModelPath(data.ModelPath, id, "modelPath");
            var maleModelPath = OptionalModelPath(data.MaleModelPath, id, "maleModelPath");
            var femaleModelPath = OptionalModelPath(data.FemaleModelPath, id, "femaleModelPath");
            var hasModel = modelPath is not null || maleModelPath is not null || femaleModelPath is not null;
            var modelHeight = Positive(data.ModelHeight, id, "modelHeight", required: hasModel);
            var modelRadius = Positive(data.ModelCollisionRadius, id, "modelCollisionRadius", required: hasModel);
            var fallbackHeight = Positive(data.FallbackCollisionHeight, id, "fallbackCollisionHeight", required: true);
            var fallbackRadius = Positive(data.FallbackCollisionRadius, id, "fallbackCollisionRadius", required: true);
            var bodySize = Vector(data.ProceduralBodySize, id, "proceduralBodySize", positive: true);
            var bodyColor = ColorValue(data.BodyColor, id, "bodyColor");
            var accentColor = ColorValue(data.AccentColor, id, "accentColor");
            var lowerBodyColor = ColorValue(data.LowerBodyColor, id, "lowerBodyColor");
            var family = string.IsNullOrWhiteSpace(data.LocomotionFamily)
                ? throw new InvalidDataException($"Presentation '{id}' requires a locomotionFamily.")
                : data.LocomotionFamily.Trim().ToLowerInvariant();
            var displayName = string.IsNullOrWhiteSpace(data.DisplayName) ? id : data.DisplayName.Trim();
            var upright = data.Upright is null
                ? UprightPresentation.Default
                : UprightPresentation.Create(data.Upright, id);

            return new EntityPresentationDefinition(id, modelPath, maleModelPath, femaleModelPath,
                displayName, data.MaleDisplayName?.Trim(), modelHeight, modelRadius,
                bodyColor, accentColor, lowerBodyColor, data.VaryPrimaryColorByEntity,
                bodySize, fallbackHeight, fallbackRadius, family, upright);
        }

        internal static EntityPresentationDefinition CreateFallback(CreatureBodyPlan bodyPlan, bool isCreature)
        {
            var bodySize = bodyPlan switch
            {
                CreatureBodyPlan.Ungulate => new Vector3(0.88f, 0.78f, 1.4f),
                CreatureBodyPlan.Canid => new Vector3(0.6f, 0.5f, 1.2f),
                CreatureBodyPlan.Primate => new Vector3(0.82f, 0.62f, 1.15f),
                _ => new Vector3(0.75f, 0.65f, 1.25f)
            };
            var fallbackHeight = bodyPlan == CreatureBodyPlan.Humanoid
                ? UprightPresentation.Default.TotalHeight
                : Mathf.Max(bodySize.Y * 0.85f, bodySize.Y * 0.75f) + bodySize.Y;
            var fallbackRadius = bodyPlan == CreatureBodyPlan.Humanoid
                ? 0.32f
                : Mathf.Min(Mathf.Max(bodySize.X, bodySize.Z) * 0.4f, fallbackHeight * 0.5f);
            return new EntityPresentationDefinition(
                "fallback", null, null, null, "FallbackModel", null,
                0, 0,
                isCreature ? new Color(0.6f, 0.45f, 0.3f) : new Color(0.5f, 0.4f, 0.35f),
                new Color(0.35f, 0.25f, 0.15f), new Color(0.22f, 0.25f, 0.36f),
                !isCreature, bodySize, fallbackHeight, fallbackRadius,
                bodyPlan.ToString().ToLowerInvariant(), UprightPresentation.Default);
        }

        private static string? OptionalModelPath(string? value, string id, string field)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            var path = value.Trim();
            if (!path.StartsWith("res://", StringComparison.Ordinal))
                throw new InvalidDataException($"Presentation '{id}' {field} must be a Godot res:// path.");
            return path;
        }

        private static float Positive(float? value, string id, string field, bool required)
        {
            if (value is null && !required) return 0;
            if (value is not { } number || !float.IsFinite(number) || number <= 0)
                throw new InvalidDataException($"Presentation '{id}' requires a finite positive {field}.");
            return number;
        }

        private static Vector3 Vector(float[]? values, string id, string field, bool positive)
        {
            if (values is not { Length: 3 } || values.Any(value => !float.IsFinite(value))
                || (positive && values.Any(value => value <= 0)))
                throw new InvalidDataException($"Presentation '{id}' {field} must contain three finite {(positive ? "positive " : string.Empty)}values.");
            return new Vector3(values[0], values[1], values[2]);
        }

        private static Color ColorValue(float[]? values, string id, string field)
        {
            if (values is not { Length: 3 or 4 } || values.Any(value => !float.IsFinite(value) || value is < 0 or > 1))
                throw new InvalidDataException($"Presentation '{id}' {field} must contain three or four values in [0, 1].");
            return new Color(values[0], values[1], values[2], values.Length == 4 ? values[3] : 1);
        }
    }

    public sealed record UprightPresentation(
        float LegLength,
        float ShoulderWidth,
        Vector3 ChestSize,
        float ArmLength,
        float ForwardLeanRadians,
        float BobHeight,
        float ArmThickness,
        float LegThickness)
    {
        public static UprightPresentation Default { get; } = new(0.9f, 0.5f,
            new Vector3(0.5f, 0.55f, 0.28f), 0.68f, 0, 0.035f, 0.11f, 0.15f);

        internal static UprightPresentation Create(UprightPresentationData data, string id)
        {
            var legLength = Positive(data.LegLength, id, "upright.legLength");
            var shoulderWidth = Positive(data.ShoulderWidth, id, "upright.shoulderWidth");
            var chest = Vector(data.ChestSize, id, "upright.chestSize");
            var armLength = Positive(data.ArmLength, id, "upright.armLength");
            var lean = data.ForwardLeanRadians ?? 0;
            var bob = data.BobHeight ?? 0.035f;
            var armThickness = Positive(data.ArmThickness, id, "upright.armThickness");
            var legThickness = Positive(data.LegThickness, id, "upright.legThickness");
            if (!float.IsFinite(lean) || !float.IsFinite(bob) || bob < 0)
                throw new InvalidDataException($"Presentation '{id}' has invalid upright lean or bob height.");
            return new UprightPresentation(legLength, shoulderWidth, chest, armLength, lean, bob, armThickness, legThickness);
        }

        public float TotalHeight => LegLength + ChestSize.Y + 0.35f;

        private static float Positive(float? value, string id, string field)
        {
            if (value is not { } number || !float.IsFinite(number) || number <= 0)
                throw new InvalidDataException($"Presentation '{id}' requires a finite positive {field}.");
            return number;
        }

        private static Vector3 Vector(float[]? values, string id, string field)
        {
            if (values is not { Length: 3 } || values.Any(value => !float.IsFinite(value) || value <= 0))
                throw new InvalidDataException($"Presentation '{id}' {field} must contain three finite positive values.");
            return new Vector3(values[0], values[1], values[2]);
        }
    }
}
