using System.Text.RegularExpressions;
using YenSTT.Core;

namespace YenSTT.Utils;

/// <summary>
/// Обработчик текста: команды пунктуации и форматирование
/// </summary>
public static class TextProcessor
{
    /// <summary>
    /// Словарь голосовых команд пунктуации
    /// </summary>
    private static readonly Dictionary<string, string> PunctuationCommands = new()
    {
        { "точка", "." },
        { "запятая", "," },
        { "вопросительный знак", "?" },
        { "восклицательный знак", "!" },
        { "восклицательный", "!" },
        { "вопросительный", "?" },
        { "двоеточие", ":" },
        { "точка с запятой", ";" },
        { "тире", "—" },
        { "дефис", "-" },
        { "новая строка", "\n" },
        { "абзац", "\n\n" },
        { "открывающая кавычка", "\"" },
        { "закрывающая кавычка", "\"" },
        { "кавычки", "\"" },
        { "скобка открывающая", "(" },
        { "скобка закрывающая", ")" },
        { "открывающая скобка", "(" },
        { "закрывающая скобка", ")" },
        { "многоточие", "..." },
        { "процент", "%" },
        { "плюс", "+" },
        { "минус", "-" },
        { "равно", "=" },
        { "слэш", "/" },
        { "бэкслэш", "\\" },
        { "собака", "@" },
        { "решётка", "#" },
        { "доллар", "$" },
        { "евро", "€" },
        { "стоп", "" }, // Удаление слова "стоп"
    };

    /// <summary>
    /// Обработать текст: применить команды пунктуации
    /// </summary>
    public static string Process(string text, AppConfig config)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        var result = text;

        // Применяем команды пунктуации
        if (config.EnablePunctuationCommands)
        {
            result = ApplyPunctuationCommands(result);
        }

        // Убираем лишние пробелы
        result = Regex.Replace(result, @"[ \t]+", " ");
        result = Regex.Replace(result, @"\n{3,}", "\n\n");

        // Добавляем пробелы вокруг знаков препинания если нужно
        result = NormalizePunctuationSpacing(result);

        // Делаем первую букву заглавной
        result = CapitalizeFirst(result);

        return result.Trim();
    }

    /// <summary>
    /// Применить команды пунктуации
    /// </summary>
    private static string ApplyPunctuationCommands(string text)
    {
        var result = text;

        // Сортируем по длине (сначала длинные фразы)
        var sortedCommands = PunctuationCommands
            .OrderByDescending(c => c.Key.Length)
            .ToList();

        foreach (var command in sortedCommands)
        {
            // Ищем команду как отдельное слово (с учётом границ слова)
            var pattern = $@"\b{Regex.Escape(command.Key)}\b";
            var replacement = string.IsNullOrEmpty(command.Value) ? "" : command.Value;

            // Для знаков препинания добавляем пробел до или после
            if (!string.IsNullOrEmpty(replacement) && !char.IsLetterOrDigit(replacement[0]) && replacement != "\n")
            {
                // Убираем пробел перед знаком препинания, добавляем после
                result = Regex.Replace(result, $@"\s*{pattern}\s*", $" {replacement} ");
            }
            else if (replacement == "\n" || replacement == "\n\n")
            {
                // Новая строка - убираем лишние пробелы
                result = Regex.Replace(result, $@"\s*{pattern}\s*", replacement);
            }
            else
            {
                result = Regex.Replace(result, pattern, replacement, RegexOptions.IgnoreCase);
            }
        }

        return result;
    }

    /// <summary>
    /// Нормализовать пробелы вокруг знаков препинания
    /// </summary>
    private static string NormalizePunctuationSpacing(string text)
    {
        var result = text;

        // Убираем пробелы перед знаками препинания
        result = Regex.Replace(result, @"\s+([.,!?;:)])", "$1");

        // Добавляем пробелы после знаков препинания (если нет)
        result = Regex.Replace(result, @"([.,!?;:])([^\s\d\n""\)\]\.,!?;:])", "$1 $2");

        // Добавляем пробел перед открывающей скобкой/кавычкой
        result = Regex.Replace(result, @"([^\s\n\(\[""])([\(\[""])", "$1 $2");

        return result;
    }

    /// <summary>
    /// Сделать первую букву заглавной
    /// </summary>
    private static string CapitalizeFirst(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        // Находим первую букву
        for (int i = 0; i < text.Length; i++)
        {
            if (char.IsLetter(text[i]))
            {
                return text.Substring(0, i) + char.ToUpper(text[i]) + text.Substring(i + 1);
            }
        }

        return text;
    }

    /// <summary>
    /// Добавить форматирование для вставки
    /// </summary>
    public static string PrepareForInsertion(string text, AppConfig config)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        var result = text;

        // Добавить пробел перед
        if (config.AddSpaceBefore)
        {
            result = " " + result;
        }

        // Добавить пробел после
        if (config.AddSpaceAfter)
        {
            result = result + " ";
        }

        return result;
    }

    /// <summary>
    /// Получить список поддерживаемых команд пунктуации для справки
    /// </summary>
    public static string GetCommandsHelp()
    {
        var help = "Поддерживаемые голосовые команды:\n\n";

        var commands = new List<(string command, string result)>
        {
            ("точка", "."),
            ("запятая", ","),
            ("вопросительный знак", "?"),
            ("восклицательный знак", "!"),
            ("двоеточие", ":"),
            ("точка с запятой", ";"),
            ("тире", "—"),
            ("новая строка", "перенос строки"),
            ("абзац", "новый абзац"),
            ("кавычки", "\""),
            ("скобка открывающая", "("),
            ("скобка закрывающая", ")"),
        };

        foreach (var cmd in commands)
        {
            help += $"• \"{cmd.command}\" → {cmd.result}\n";
        }

        return help;
    }
}
