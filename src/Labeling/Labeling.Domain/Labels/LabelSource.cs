namespace Labeling.Domain.Labels;

public enum LabelSource
{
    /// <summary>Метка — итог голосования учителей (возможно, подтверждённый человеком).</summary>
    Consensus,

    /// <summary>Метку исправил разметчик.</summary>
    Human,
}
