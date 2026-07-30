namespace YenSTT.Core;

public sealed record WhisperModelInfo(
    string Name,
    string DisplayName,
    string DownloadSize,
    string Details)
{
    public override string ToString() => DisplayName;
}

public static class WhisperModels
{
    public const string DefaultName = "small";

    public static IReadOnlyList<WhisperModelInfo> All { get; } =
    [
        new("tiny", "tiny - минимальная", "~80 МБ", "Самая быстрая, базовое качество, рекомендуется 4 ГБ ОЗУ"),
        new("base", "base - базовая", "~150 МБ", "Быстрая, качество немного выше tiny, рекомендуется 4 ГБ ОЗУ"),
        new("small", "small - сбалансированная (рекомендуется)", "~500 МБ", "Хороший баланс качества и скорости, рекомендуется 8 ГБ ОЗУ"),
        new("medium", "medium - повышенное качество", "~1,5 ГБ", "Точнее small, заметно медленнее на CPU, рекомендуется 12 ГБ ОЗУ"),
        new("large-v3", "large-v3 - максимальное качество", "~3,1 ГБ", "Наиболее точная, очень высокая нагрузка на CPU, рекомендуется 16 ГБ ОЗУ")
    ];

    public static bool IsSupported(string? name) =>
        All.Any(model => string.Equals(model.Name, name, StringComparison.OrdinalIgnoreCase));

    public static WhisperModelInfo Get(string? name) =>
        All.FirstOrDefault(model => string.Equals(model.Name, name, StringComparison.OrdinalIgnoreCase))
        ?? All.First(model => model.Name == DefaultName);

    public static string Normalize(string? name) => Get(name).Name;
}
