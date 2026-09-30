namespace Simulation;

public sealed class WorkstationRecipe
{
    private readonly IReadOnlyDictionary<string,int> _inputs;
    private readonly IReadOnlyDictionary<string,int> _outputs;
    public WorkstationRecipe(string id, IReadOnlyDictionary<string,int> inputs, IReadOnlyDictionary<string,int> outputs, int workRequired)
    {
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("A recipe ID is required.", nameof(id));
        if (workRequired <= 0) throw new ArgumentOutOfRangeException(nameof(workRequired));
        ValidateMaterials(inputs, nameof(inputs)); ValidateMaterials(outputs, nameof(outputs));
        if (outputs.Count == 0) throw new ArgumentException("At least one output is required.", nameof(outputs));
        Id=id; WorkRequired=workRequired; _inputs=new Dictionary<string,int>(inputs,StringComparer.Ordinal); _outputs=new Dictionary<string,int>(outputs,StringComparer.Ordinal);
    }
    public string Id { get; }
    public int WorkRequired { get; }
    public int WorkProgress { get; private set; }
    public bool IsComplete => WorkProgress >= WorkRequired;
    public IReadOnlyDictionary<string,int> Inputs => _inputs;
    public IReadOnlyDictionary<string,int> Outputs => _outputs;
    public bool ApplyWork(int amount)
    {
        if (amount <= 0 || IsComplete) return false;
        WorkProgress=Math.Min(WorkRequired,WorkProgress+amount); return true;
    }
    private static void ValidateMaterials(IReadOnlyDictionary<string,int> materials,string parameter)
    {
        ArgumentNullException.ThrowIfNull(materials);
        if (materials.Any(item => !ItemIds.IsKnown(item.Key) || item.Value <= 0)) throw new ArgumentException("Materials must use known items with positive quantities.", parameter);
    }
}
