namespace CamAda.Core.Safety;

/// <summary>
/// Görev çubuğunun bizim tarafımızdan gizlenip gizlenmediği ve son çıkışın temiz olup olmadığı.
/// <paramref name="OriginalTaskbarState"/>: gizlemeden önceki ABM_GETSTATE değeri (1 = otomatik gizle, 2 = her zaman üstte).
/// </summary>
public sealed record TaskbarSafetyState(bool HiddenByUs, bool CleanShutdown, int OriginalTaskbarState);
