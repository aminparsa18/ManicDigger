/// <summary>
/// Serializes and deserializes tab-separated, section-based data files
/// into a model via an <see cref="ITableBinding"/>.
/// </summary>
public class TableSerializer
{
    private const string SectionPrefix = "section=";
    private const string TabSeparator = "\t";

    /// <summary>
    /// Parses <paramref name="data"/> and populates the model
    /// through <paramref name="binding"/>.
    /// </summary>
    /// <exception cref="FormatException">
    /// Thrown when a section header is missing or a row has more
    /// columns than the header.
    /// </exception>
    public static void Deserialize(GamePlatform p, string data, ITableBinding binding)
    {
        string[] lines = p.ReadAllLines(data, out int linesCount);
        string[] header = null;
        string section = "";
        int rowIndex = 0;

        for (int i = 0; i < linesCount; i++)
        {
            string line = p.StringTrim(lines[i]);

            if (line == "") { continue; }
            if (p.StringStartsWithIgnoreCase(line, "//")) { continue; }
            if (p.StringStartsWithIgnoreCase(line, "#")) { continue; }

            if (p.StringStartsWithIgnoreCase(line, SectionPrefix))
            {
                section = p.StringReplace(line, SectionPrefix, "");

                if (i + 1 >= linesCount)
                {
                    throw new FormatException($"Section '{section}' has no header row.");
                }

                header = p.StringSplit(p.StringTrim(lines[++i]), TabSeparator, out _);
                rowIndex = 0;
                continue;
            }

            if (header == null)
            {
                throw new FormatException($"Data row found before any section declaration: '{line}'");
            }

            string[] columns = p.StringSplit(line, TabSeparator, out int columnCount);

            if (columnCount > header.Length)
            {
                throw new FormatException(
                    $"Row {rowIndex} in section '{section}' has {columnCount} columns but header has {header.Length}.");
            }

            for (int k = 0; k < columnCount; k++)
            {
                binding.Set(section, rowIndex, header[k], columns[k]);
            }

            rowIndex++;
        }
    }
}
